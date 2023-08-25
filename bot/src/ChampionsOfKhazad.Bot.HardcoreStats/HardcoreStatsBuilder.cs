using Microsoft.Extensions.DependencyInjection;

namespace ChampionsOfKhazad.Bot.HardcoreStats;

public class HardcoreStatsBuilder
{
    public IServiceCollection Services { get; }

    internal HardcoreStatsBuilder(IServiceCollection services)
    {
        Services = services;
    }
}
