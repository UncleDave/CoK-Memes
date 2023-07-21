using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot;

public class BotService : IHostedService
{
    private readonly DiscordSocketClient _client;
    private readonly ILogger _logger;
    private readonly IEnumerable<IEventHandler> _eventHandlers;
    private readonly BotOptions _options;

    private IEnumerable<IMessageReceivedEventHandler> _messageReceivedEventHandlers =
        Array.Empty<IMessageReceivedEventHandler>();

    private IEnumerable<IReactionAddedEventHandler> _reactionAddedEventHandlers =
        Array.Empty<IReactionAddedEventHandler>();

    public BotService(
        DiscordSocketClient client,
        ILogger<BotService> logger,
        IOptions<BotOptions> options,
        IEnumerable<IEventHandler> eventHandlers
    )
    {
        _client = client;
        _logger = logger;
        _options = options.Value;
        _eventHandlers = eventHandlers;

        _client.Ready += Ready;
        _client.MessageReceived += MessageReceivedAsync;
        _client.ReactionAdded += ReactionAddedAsync;
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
        _logger.LogDebug(
            "Guild: {Guild}, channels: {Channels}",
            guild.Name,
            guild.Channels.Select(x => x.Name)
        );

        var context = new BotContext(_client.Rest.CurrentUser.Id, guild);

        var startedEventHandlers = (
            await StartEventHandlersAsync(_eventHandlers, context)
        ).ToList();

        _messageReceivedEventHandlers = startedEventHandlers.OfType<IMessageReceivedEventHandler>();
        _reactionAddedEventHandlers = startedEventHandlers.OfType<IReactionAddedEventHandler>();

        _logger.LogInformation("Bot started");
    }

    private async Task MessageReceivedAsync(SocketMessage message)
    {
        if (message is not SocketUserMessage userMessage || message.Author.IsBot)
            return;

        foreach (var handler in _messageReceivedEventHandlers)
        {
            try
            {
                await handler.HandleMessageAsync(userMessage);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error handling message with {EventHandler}", handler);
            }
        }
    }

    private async Task ReactionAddedAsync(
        Cacheable<IUserMessage, ulong> message,
        Cacheable<IMessageChannel, ulong> channel,
        SocketReaction reaction
    )
    {
        foreach (var handler in _reactionAddedEventHandlers)
        {
            try
            {
                await handler.HandleReactionAsync(reaction);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error handling reaction with {EventHandler}", handler);
            }
        }
    }

    private Task<IEnumerable<IEventHandler>> StartEventHandlersAsync(
        IEnumerable<IEventHandler> eventHandlers,
        BotContext context
    ) =>
        eventHandlers.WhereAsync(async eventHandler =>
        {
            try
            {
                await eventHandler.StartAsync(context);
                _logger.LogDebug("Started event handler {EventHandler}", eventHandler);
                return true;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error starting event handler {EventHandler}", eventHandler);
                return false;
            }
        });
}
