namespace ChampionsOfKhazad.Bot.Logging;

public sealed class DiscordLoggerConfiguration
{
    public ulong WebhookId { get; set; }
    public string WebhookToken { get; set; } = string.Empty;
    public Microsoft.Extensions.Logging.LogLevel MinimumLevel { get; set; } = Microsoft.Extensions.Logging.LogLevel.Error;
}
