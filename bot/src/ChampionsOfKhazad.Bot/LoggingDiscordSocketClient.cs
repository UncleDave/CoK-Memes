using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot;

public class LoggingDiscordSocketClient : DiscordSocketClient
{
    private readonly ILogger<LoggingDiscordSocketClient> _logger;
    private readonly BotOptions _options;
    private bool _isReady;

    public LoggingDiscordSocketClient(ILogger<LoggingDiscordSocketClient> logger, IOptions<BotOptions> options, DiscordSocketConfig? config = null)
        : base(config)
    {
        _logger = logger;
        _options = options.Value;

        Ready += ReadyAsync;
        Log += LogAsync;
    }

    private async Task ReadyAsync()
    {
        if (_options.StartMessageUserId.HasValue && !_isReady)
        {
            var startMessageTargetUser = await GetUserAsync(_options.StartMessageUserId.Value);
            await startMessageTargetUser.SendMessageAsync("Bot started");

            _isReady = true;
        }
    }

    private Task LogAsync(LogMessage message)
    {
        var severity = message.Severity switch
        {
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Error => LogLevel.Error,
            LogSeverity.Warning => LogLevel.Warning,
            LogSeverity.Info => LogLevel.Information,
            LogSeverity.Verbose => LogLevel.Trace,
            LogSeverity.Debug => LogLevel.Debug,
            _ => LogLevel.Information,
        };

        _logger.Log(severity, message.Exception, "[{Source}] {Message}", message.Source, message.Message);

        return Task.CompletedTask;
    }
}
