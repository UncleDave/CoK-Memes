using ChampionsOfKhazad.Bot.Core;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public class BotBuilder
{
    public IServiceCollection Services { get; }
    public BotConfiguration BotConfiguration { get; }

    public BotBuilder(IServiceCollection services, BotConfiguration botConfiguration)
    {
        Services = services;
        BotConfiguration = botConfiguration;
    }
}
