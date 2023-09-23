using Discord;
using Discord.WebSocket;
using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot;

public class BotService : IHostedService
{
    private readonly DiscordSocketClient _client;
    private readonly ILogger<BotService> _logger;
    private readonly BotOptions _options;
    private readonly BotContextProvider _botContextProvider;
    private readonly IPublisher _publisher;

    public BotService(
        DiscordSocketClient client,
        ILogger<BotService> logger,
        IOptions<BotOptions> options,
        BotContextProvider botContextProvider,
        IPublisher publisher
    )
    {
        _client = client;
        _logger = logger;
        _options = options.Value;
        _botContextProvider = botContextProvider;
        _publisher = publisher;

        _client.Ready += Ready;
        _client.MessageReceived += MessageReceivedAsync;
        _client.ReactionAdded += ReactionAddedAsync;
        _client.SlashCommandExecuted += SlashCommandExecuted;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Bot");

        await _client.LoginAsync(TokenType.Bot, _options.Token);

        cancellationToken.ThrowIfCancellationRequested();

        await _client.StartAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken) => await _client.StopAsync();

    private async Task Ready()
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

        if (_options.StartMessageUserId.HasValue)
        {
            var startMessageTargetUser = await _client.GetUserAsync(_options.StartMessageUserId.Value);
            var message = _options.CommitSha is not null
                ? $"Bot started, commit: [{_options.CommitSha}]({Constants.RepositoryUrl}/commit/{_options.CommitSha})"
                : "Bot started";

            await startMessageTargetUser.SendMessageAsync(message);
        }
    }

    private Task MessageReceivedAsync(SocketMessage message)
    {
        if (message is not SocketUserMessage userMessage || message.Author.IsBot)
            return Task.CompletedTask;

        return _publisher.Publish(new MessageReceived(userMessage));
    }

    private Task ReactionAddedAsync(Cacheable<IUserMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction) =>
        _publisher.Publish(new ReactionAdded(reaction));

    private Task SlashCommandExecuted(SocketSlashCommand command)
    {
        var notification = SlashCommands.All.Single(x => x.Properties.Name.Value == command.CommandName).CreateNotification(command);
        return _publisher.Publish(notification);
    }
}
