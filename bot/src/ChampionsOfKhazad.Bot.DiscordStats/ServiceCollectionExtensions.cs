using ChampionsOfKhazad.Bot.DiscordStats;
using ChampionsOfKhazad.Bot.DiscordStats.StreakBreaks;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static DiscordStatsBuilder AddDiscordStats(this IServiceCollection services)
    {
        services
            .AddMediatR(configuration =>
            {
                configuration.RegisterServicesFromAssemblyContaining<StreakBreak>();
            })
            .AddSingleton<IGetStreakBreaks, StreakBreakService>();

        return new DiscordStatsBuilder(services);
    }
}
