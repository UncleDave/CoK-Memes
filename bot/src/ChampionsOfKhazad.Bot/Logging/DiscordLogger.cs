using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ChampionsOfKhazad.Bot.Logging;

public class DiscordLogger(string categoryName, DiscordWebhookClient webhookClient, DiscordLoggerConfiguration configuration) : ILogger
{
    private readonly string _categoryName = categoryName;
    private readonly DiscordWebhookClient _webhookClient = webhookClient;
    private readonly DiscordLoggerConfiguration _configuration = configuration;

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => default;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= _configuration.MinimumLevel;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var message = formatter(state, exception);
        if (string.IsNullOrEmpty(message) && exception == null)
        {
            return;
        }

        _ = _webhookClient.SendLogMessageAsync(logLevel, _categoryName, message, exception);
    }
}
