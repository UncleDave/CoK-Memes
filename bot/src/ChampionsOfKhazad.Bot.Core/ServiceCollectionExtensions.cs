using ChampionsOfKhazad.Bot.Core;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static BotBuilder AddBot(this IServiceCollection services, Action<BotConfiguration> configureBot)
    {
        var botConfiguration = new BotConfiguration();
        configureBot(botConfiguration);

        return new BotBuilder(services, botConfiguration);
    }
}
