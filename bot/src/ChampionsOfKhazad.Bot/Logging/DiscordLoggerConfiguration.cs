using Microsoft.Extensions.Logging;

namespace ChampionsOfKhazad.Bot.Logging;

public class DiscordLoggerConfiguration
{
    public ulong WebhookId { get; set; }
    public string WebhookToken { get; set; } = string.Empty;
    public LogLevel MinimumLevel { get; set; } = LogLevel.Error;
}
