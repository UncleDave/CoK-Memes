using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;

namespace ChampionsOfKhazad.Bot.Logging;

public static class DiscordLoggerExtensions
{
    public static ILoggingBuilder AddDiscord(this ILoggingBuilder builder, Action<DiscordLoggerConfiguration> configure)
    {
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, DiscordLoggerProvider>());
        builder.Services.Configure(configure);
        LoggerProviderOptions.RegisterProviderOptions<DiscordLoggerConfiguration, DiscordLoggerProvider>(builder.Services);
        return builder;
    }
}
