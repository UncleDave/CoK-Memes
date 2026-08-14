using Discord;
using Discord.WebSocket;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot;

public class BotService : IHostedService
{
    private readonly DiscordSocketClient _client;
    private readonly ILogger<BotService> _logger;
    private readonly BotOptions _options;
    private readonly IServiceProvider _serviceProvider;
    private readonly BotContextProvider _botContextProvider;

    public BotService(
        DiscordSocketClient client,
        ILogger<BotService> logger,
        IOptions<BotOptions> options,
        IServiceProvider serviceProvider,
        BotContextProvider botContextProvider
    )
    {
        _client = client;
        _logger = logger;
        _options = options.Value;
        _serviceProvider = serviceProvider;
        _botContextProvider = botContextProvider;

        _client.Ready += ReadyAsync;
        _client.MessageReceived += MessageReceivedAsync;
        _client.ReactionAdded += ReactionAddedAsync;
        _client.SlashCommandExecuted += SlashCommandExecutedAsync;
        _client.UserLeft += UserLeftAsync;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Bot");

        await _client.LoginAsync(TokenType.Bot, _options.Token);

        cancellationToken.ThrowIfCancellationRequested();

        await _client.StartAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _client.StopAsync();
    }

    private async Task ReadyAsync()
    {
        var guild = _client.GetGuild(_options.GuildId);

        _logger.LogDebug("Guilds: {Guilds}", _client.Guilds.Select(x => x.Id));
        _logger.LogDebug("Guild: {Guild}, channels: {Channels}", guild.Name, guild.Channels.Select(x => x.Name));

        _botContextProvider.BotContext = new BotContext(_client.CurrentUser.Id, guild, _client);

        foreach (var slashCommand in SlashCommands.GuildCommands)
            await guild.CreateApplicationCommandAsync(slashCommand.Properties);

        foreach (var slashCommand in SlashCommands.GlobalCommands)
            await _client.CreateGlobalApplicationCommandAsync(slashCommand.Properties);

        _logger.LogInformation("Bot started");
    }

    private async Task MessageReceivedAsync(SocketMessage message)
    {
        if (message is not SocketUserMessage userMessage || message.Author.IsBot)
            return;

        await PublishInBackground(new MessageReceived(userMessage));
    }

    private async Task ReactionAddedAsync(
        Cacheable<IUserMessage, ulong> message,
        Cacheable<IMessageChannel, ulong> channel,
        SocketReaction reaction
    ) => await PublishInBackground(new ReactionAdded(reaction));

    private async Task SlashCommandExecutedAsync(SocketSlashCommand command)
    {
        var notification = SlashCommands.All.Single(x => x.Properties.Name.Value == command.CommandName).CreateNotification(command);
        await PublishInBackground(notification);
    }

    private async Task UserLeftAsync(SocketGuild guild, SocketUser user) => await PublishInBackground(new UserLeft(user));

    private Task PublishInBackground(INotification notification)
    {
        _ = Task.Run(() => PublishWithScopeAsync(notification));

        return Task.CompletedTask;
    }

    private async Task PublishWithScopeAsync(INotification notification)
    {
        try
        {
            await using var messageScope = _serviceProvider.CreateAsyncScope();
            var publisher = messageScope.ServiceProvider.GetRequiredService<IPublisher>();
            await publisher.Publish(notification);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error publishing notification {NotificationType}", notification.GetType().Name);
        }
    }
}
