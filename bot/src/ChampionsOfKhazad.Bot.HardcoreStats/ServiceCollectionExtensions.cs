using ChampionsOfKhazad.Bot.HardcoreStats;
using ChampionsOfKhazad.Bot.HardcoreStats.CharacterDeaths;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static HardcoreStatsBuilder AddHardcoreStats(this IServiceCollection services)
    {
        services
            .AddMediatR(configuration =>
            {
                configuration.RegisterServicesFromAssemblyContaining<CharacterDeath>();
            })
            .AddSingleton<IRecordCharacterDeaths, CharacterDeathService>();

        return new HardcoreStatsBuilder(services);
    }
}
