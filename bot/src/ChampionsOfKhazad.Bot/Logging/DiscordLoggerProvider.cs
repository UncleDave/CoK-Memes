using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot.Logging;

[ProviderAlias("Discord")]
public sealed class DiscordLoggerProvider : ILoggerProvider
{
    private readonly DiscordLoggerConfiguration _configuration;
    private readonly ConcurrentDictionary<string, DiscordLogger> _loggers = new();
    private readonly DiscordWebhookClient _webhookClient;

    public DiscordLoggerProvider(IOptions<DiscordLoggerConfiguration> configuration)
    {
        _configuration = configuration.Value;
        _webhookClient = new DiscordWebhookClient(_configuration);
    }

    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(categoryName, name => new DiscordLogger(name, _webhookClient, _configuration));
    }

    public void Dispose()
    {
        _loggers.Clear();
        _webhookClient.Dispose();
    }
}
