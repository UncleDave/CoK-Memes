using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot.Logging;

[ProviderAlias("Discord")]
public class DiscordLoggerProvider(IOptions<DiscordLoggerConfiguration> configuration) : ILoggerProvider
{
    private readonly DiscordLoggerConfiguration _configuration = configuration.Value;
    private readonly ConcurrentDictionary<string, DiscordLogger> _loggers = new();
    private readonly DiscordWebhookClient _webhookClient = new(configuration.Value);

    public ILogger CreateLogger(string categoryName) =>
        _loggers.GetOrAdd(categoryName, name => new DiscordLogger(name, _webhookClient, _configuration));

    public void Dispose()
    {
        _loggers.Clear();
        _webhookClient.Dispose();
    }
}
