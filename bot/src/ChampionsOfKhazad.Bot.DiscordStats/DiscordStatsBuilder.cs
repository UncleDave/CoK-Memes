using Microsoft.Extensions.DependencyInjection;

namespace ChampionsOfKhazad.Bot.DiscordStats;

public class DiscordStatsBuilder
{
    public IServiceCollection Services { get; }

    internal DiscordStatsBuilder(IServiceCollection services)
    {
        Services = services;
    }
}
