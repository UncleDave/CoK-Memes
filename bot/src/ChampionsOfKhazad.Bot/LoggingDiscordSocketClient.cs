using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace ChampionsOfKhazad.Bot;

public class LoggingDiscordSocketClient : DiscordSocketClient
{
    private readonly ILogger<LoggingDiscordSocketClient> _logger;

    public LoggingDiscordSocketClient(ILogger<LoggingDiscordSocketClient> logger, DiscordSocketConfig? config = null)
        : base(config)
    {
        _logger = logger;

        Log += LogAsync;
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
            _ => LogLevel.Information
        };

        _logger.Log(severity, message.Exception, "[{Source}] {Message}", message.Source, message.Message);

        return Task.CompletedTask;
    }
}
