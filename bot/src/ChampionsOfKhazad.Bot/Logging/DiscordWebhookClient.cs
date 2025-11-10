using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ChampionsOfKhazad.Bot.Logging;

public class DiscordWebhookClient(DiscordLoggerConfiguration configuration) : IDisposable
{
    private readonly HttpClient _httpClient = new();
    private readonly string _webhookUrl = $"https://discord.com/api/webhooks/{configuration.WebhookId}/{configuration.WebhookToken}";

    public async Task SendLogMessageAsync(LogLevel logLevel, string categoryName, string message, Exception? exception)
    {
        try
        {
            var embed = new
            {
                title = $"{GetLogLevelEmoji(logLevel)} {logLevel} - {categoryName}",
                description = BuildDescription(message, exception),
                color = GetLogLevelColor(logLevel),
                timestamp = DateTime.UtcNow.ToString("o"),
            };

            var payload = new { embeds = new[] { embed } };

            var response = await _httpClient.PostAsJsonAsync(_webhookUrl, payload);
            response.EnsureSuccessStatusCode();
        }
        catch
        {
            // Silently fail - we don't want logging errors to crash the application
        }
    }

    private static string BuildDescription(string message, Exception? exception)
    {
        var description = new StringBuilder();

        if (!string.IsNullOrEmpty(message))
        {
            description.AppendLine($"**Message:** {message}");
        }

        if (exception != null)
        {
            description.AppendLine($"**Exception:** {exception.GetType().Name}");
            description.AppendLine($"**Message:** {exception.Message}");

            if (!string.IsNullOrEmpty(exception.StackTrace))
            {
                var stackTrace = exception.StackTrace.Length > 1000 ? exception.StackTrace.Substring(0, 1000) + "..." : exception.StackTrace;
                description.AppendLine($"**Stack Trace:**\n```\n{stackTrace}\n```");
            }
        }

        // Discord embed descriptions are limited to 4096 characters
        var result = description.ToString();
        return result.Length > 4096 ? result.Substring(0, 4093) + "..." : result;
    }

    private static string GetLogLevelEmoji(LogLevel logLevel) =>
        logLevel switch
        {
            LogLevel.Critical => "🔥",
            LogLevel.Error => "❌",
            LogLevel.Warning => "⚠️",
            LogLevel.Information => "ℹ️",
            LogLevel.Debug => "🐛",
            LogLevel.Trace => "📝",
            _ => "📋",
        };

    private static int GetLogLevelColor(LogLevel logLevel) =>
        logLevel switch
        {
            LogLevel.Critical => 0x8B0000, // Dark red
            LogLevel.Error => 0xFF0000, // Red
            LogLevel.Warning => 0xFFA500, // Orange
            LogLevel.Information => 0x0000FF, // Blue
            LogLevel.Debug => 0x808080, // Gray
            LogLevel.Trace => 0xC0C0C0, // Silver
            _ => 0x000000, // Black
        };

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
