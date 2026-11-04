using Discord;
using Discord.WebSocket;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot.Tests;

public class BotNotificationReadinessTests
{
    [Fact]
    public async Task PreReadyNotificationsNeverResolveScopedBotContextAndAcceptedWorkCanDrainAfterDisconnect()
    {
        await using var client = new DiscordSocketClient();
        var context = new BotContextProvider();
        var queue = new NotificationQueue(Options.Create(new NotificationQueueOptions()), NullLogger<NotificationQueue>.Instance);
        var bot = CreateBot(client, context, queue);
        var resolutions = 0;
        var delivered = new List<int>();
        var services = new ServiceCollection();
        services.AddScoped(_ =>
        {
            resolutions++;
            return context.BotContext ?? throw new InvalidOperationException("BotContext is not available");
        });
        services.AddScoped<IPublisher>(provider => new Publisher(provider.GetRequiredService<BotContext>(), delivered));
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var worker = new NotificationQueueService(
            queue,
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new NotificationQueueOptions()),
            NullLogger<NotificationQueueService>.Instance
        );
        await worker.StartAsync(TestContext.Current.CancellationToken);

        Assert.False(bot.TryEnqueueWhenReady(new Notification(1)));
        context.IsReady = true;
        Assert.False(bot.TryEnqueueWhenReady(new Notification(2)));
        context.BotContext = CreateContext(client);
        context.IsReady = false;
        Assert.False(bot.TryEnqueueWhenReady(new Notification(3)));
        context.IsReady = true;
        Assert.True(bot.TryEnqueueWhenReady(new Notification(4)));
        await bot.DisconnectedAsync(new IOException("Disconnected"));
        Assert.False(bot.TryEnqueueWhenReady(new Notification(5)));
        await worker.StopAsync(TestContext.Current.CancellationToken);
        Assert.Equal([4], delivered);
        Assert.Equal(1, resolutions);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task UnreadyInteractionsGetAPrivateRetryResponseInsteadOfQueuedContextDependentWork(bool ready, bool hasContext)
    {
        await using var client = new DiscordSocketClient();
        var context = new BotContextProvider { IsReady = ready, BotContext = hasContext ? CreateContext(client) : null };
        var queue = new NotificationQueue(Options.Create(new NotificationQueueOptions()), NullLogger<NotificationQueue>.Instance);
        var bot = CreateBot(client, context, queue);
        var responses = 0;
        var interaction = DiscordConversationFixture.Stub<IDiscordInteraction>(
            (method, args) =>
            {
                Assert.Equal("RespondAsync", method.Name);
                responses++;
                var parameters = method.GetParameters();
                Assert.True((bool)args![Array.FindIndex(parameters, parameter => parameter.Name == "ephemeral")]!);
                Assert.Same(AllowedMentions.None, args[Array.FindIndex(parameters, parameter => parameter.Name == "allowedMentions")]);
                return Task.CompletedTask;
            }
        );
        Assert.Equal(ready && hasContext, await bot.EnsureReadyAsync(interaction));
        Assert.Equal(ready && hasContext ? 0 : 1, responses);
    }

    private static BotService CreateBot(DiscordSocketClient client, BotContextProvider context, NotificationQueue queue) =>
        new(client, NullLogger<BotService>.Instance, Options.Create(new BotOptions { Token = "unused", GuildId = 50 }), queue, context);

    private static BotContext CreateContext(DiscordSocketClient client) =>
        new(
            42,
            DiscordConversationFixture.Stub<IGuild>((method, _) => method.Name == "get_Id" ? 50UL : throw new NotSupportedException(method.Name)),
            client
        );

    private sealed record Notification(int Id) : INotification;

    private sealed class Publisher(BotContext context, List<int> delivered) : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default)
        {
            Assert.Equal(42UL, context.BotId);
            delivered.Add(((Notification)notification).Id);
            return Task.CompletedTask;
        }

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Publish((object)notification, cancellationToken);
    }
}
