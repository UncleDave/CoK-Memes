using Discord;
using Discord.Net.WebSockets;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot.Tests;

public class DiscordClientLifetimeTests
{
    [Fact]
    public async Task ScopedDisposableAliasReproducesPrematureDisposal()
    {
        var (client, transport) = CreateClient();
        await using var ownedClient = client;
        var services = new ServiceCollection();
        services.AddScoped<IDiscordClient>(_ => client.Rest);
        await using var provider = services.BuildServiceProvider();
        await using (var scope = provider.CreateAsyncScope())
            Assert.Same(client.Rest, scope.ServiceProvider.GetRequiredService<IDiscordClient>());

        // Exercise the real Discord.Net transport, without logging in or opening a network connection.
        var exception = await Assert.ThrowsAsync<ObjectDisposedException>(() => transport.SendAsync([], 0, 0, true));
        Assert.Contains("SemaphoreSlim", exception.ObjectName);
    }

    [Fact]
    public async Task RealGatewayTransportSurvivesMessageScopesAndIsDisposedOnlyAtHostShutdown()
    {
        var (client, transport) = CreateClient();
        var services = new ServiceCollection();
        services.AddSingleton(_ => client);
        services.AddSingleton(serviceProvider => new SharedDiscordRestClient(serviceProvider.GetRequiredService<DiscordSocketClient>().Rest));
        await using (var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true }))
        {
            for (var i = 0; i < 3; i++)
            {
                await using (var scope = provider.CreateAsyncScope())
                    Assert.Same(client.Rest, scope.ServiceProvider.GetRequiredService<SharedDiscordRestClient>().Client);
                // An unconnected, live transport returns normally. A disposed one throws the production exception.
                await transport.SendAsync([], 0, 0, true);
            }
        }
        await Assert.ThrowsAsync<ObjectDisposedException>(() => transport.SendAsync([], 0, 0, true));
    }

    [Fact]
    public async Task CompletingMultipleMessageScopesDoesNotDisposeSharedRestClient()
    {
        var fixture = new DiscordConversationFixture();
        fixture.AddMessage(1, "Earlier guild conversation");
        var trigger = fixture.AddMessage(2, "Current question");
        var services = new ServiceCollection();
        services.AddSingleton(_ => new SharedDiscordRestClient(fixture.RestClient));
        services.AddSingleton<IOptions<DirectMessageHandlerOptions>>(
            Options.Create(new DirectMessageHandlerOptions { AdminUserId = DiscordConversationFixture.AdminId })
        );
        services.AddSingleton<ILogger<LorekeeperChatHistoryBuilder>>(NullLogger<LorekeeperChatHistoryBuilder>.Instance);
        services.AddScoped<LorekeeperChatHistoryBuilder>();

        await using (var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true }))
        {
            for (var i = 0; i < 3; i++)
            {
                await using (var scope = provider.CreateAsyncScope())
                {
                    var builder = scope.ServiceProvider.GetRequiredService<LorekeeperChatHistoryBuilder>();
                    var history = await builder.BuildAsync(trigger, DiscordConversationFixture.BotId, TestContext.Current.CancellationToken);
                    Assert.NotEmpty(history);
                }
                Assert.Equal(0, fixture.RestClientDisposals);
            }
        }

        // Neither message scopes nor the non-owning accessor may dispose the connection.
        Assert.Equal(0, fixture.RestClientDisposals);
        fixture.RestClient.Dispose();
        Assert.Equal(1, fixture.RestClientDisposals);
    }

    private static (DiscordSocketClient Client, IWebSocketClient Transport) CreateClient()
    {
        var config = new DiscordSocketConfig();
        var defaultProvider = config.WebSocketProvider;
        IWebSocketClient? transport = null;
        config.WebSocketProvider = () => transport = defaultProvider();
        var client = new DiscordSocketClient(config);
        return (client, transport!);
    }
}
