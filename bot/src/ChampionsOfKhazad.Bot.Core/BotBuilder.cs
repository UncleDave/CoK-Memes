using ChampionsOfKhazad.Bot.Core;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public class BotBuilder(IServiceCollection services, BotConfiguration botConfiguration)
{
    public IServiceCollection Services { get; } = services;
    public BotConfiguration BotConfiguration { get; } = botConfiguration;
}
