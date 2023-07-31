using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot;

public class BotService : IHostedService
{
    private readonly DiscordSocketClient _client;
    private readonly ILogger _logger;
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

        _botContextProvider.BotContext = new BotContext(_client.CurrentUser.Id, guild);

        foreach (var slashCommand in SlashCommands.All)
            await guild.CreateApplicationCommandAsync(slashCommand.Properties);

        _logger.LogInformation("Bot started");
    }

    private async Task ExecuteEventHandlers<T>(Func<T, Task> handlerExecutor)
    {
        using var scope = _serviceProvider.CreateScope();
        var eventHandlers = scope.ServiceProvider.GetServices<T>();

        foreach (var eventHandler in eventHandlers)
        {
            try
            {
                await handlerExecutor(eventHandler);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error executing event handler {EventHandler}", eventHandler);
            }
        }
    }

    private Task MessageReceivedAsync(SocketMessage message)
    {
        if (message is not SocketUserMessage userMessage || message.Author.IsBot)
            return Task.CompletedTask;

        return ExecuteEventHandlers<IMessageReceivedEventHandler>(eventHandler => eventHandler.HandleMessageAsync(userMessage));
    }

    private Task ReactionAddedAsync(Cacheable<IUserMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction) =>
        ExecuteEventHandlers<IReactionAddedEventHandler>(eventHandler => eventHandler.HandleReactionAsync(reaction));

    private async Task SlashCommandExecuted(SocketSlashCommand command)
    {
        using var scope = _serviceProvider.CreateScope();

        try
        {
            var commandType = SlashCommands.All.Single(x => x.Properties.Name.Value == command.CommandName).CommandType;

            var commandHandler = (ISlashCommand)scope.ServiceProvider.GetRequiredService(commandType);

            await commandHandler.ExecuteAsync(command);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error executing slash command {SlashCommand}", command.CommandName);
        }
    }
}
