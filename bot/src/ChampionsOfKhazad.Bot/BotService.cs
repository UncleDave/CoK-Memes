using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot;

public class BotService : IHostedService
{
    private readonly DiscordSocketClient _client;
    private readonly ILogger<BotService> _logger;
    private readonly BotOptions _options;
    private readonly NotificationQueue _notificationQueue;
    private readonly BotContextProvider _botContextProvider;

    public BotService(
        DiscordSocketClient client,
        ILogger<BotService> logger,
        IOptions<BotOptions> options,
        NotificationQueue notificationQueue,
        BotContextProvider botContextProvider
    )
    {
        _client = client;
        _logger = logger;
        _options = options.Value;
        _notificationQueue = notificationQueue;
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

    private Task MessageReceivedAsync(SocketMessage message)
    {
        if (message is SocketUserMessage userMessage && !message.Author.IsBot)
            _notificationQueue.TryEnqueue(new MessageReceived(userMessage));

        return Task.CompletedTask;
    }

    private Task ReactionAddedAsync(Cacheable<IUserMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
    {
        _notificationQueue.TryEnqueue(new ReactionAdded(reaction));
        return Task.CompletedTask;
    }

    private async Task SlashCommandExecutedAsync(SocketSlashCommand command)
    {
        var slashCommand = SlashCommands.All.Single(x => x.Properties.Name.Value == command.CommandName);

        // Only the initial Discord acknowledgement is awaited here, never application handlers.
        // Acknowledging before enqueueing keeps the three-second deadline independent of queue backlog.
        await slashCommand.AcknowledgeAsync(command);

        if (!_notificationQueue.TryEnqueue(slashCommand.CreateNotification(command)))
            await command.FollowupAsync("I'm busy or shutting down. Please try again shortly.", ephemeral: true);
    }

    private Task UserLeftAsync(SocketGuild guild, SocketUser user)
    {
        _notificationQueue.TryEnqueue(new UserLeft(user));
        return Task.CompletedTask;
    }
}
