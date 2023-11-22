using ChampionsOfKhazad.Bot.DiscordStats.StreakBreaks;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class DiscordStatsBotBuilderExtensions
{
    public static DiscordStatsBuilder AddDiscordStats(this BotBuilder builder)
    {
        builder
            .Services
            .AddMediatR(configuration =>
            {
                configuration.RegisterServicesFromAssemblyContaining<StreakBreak>();
            })
            .AddSingleton<IGetStreakBreaks, StreakBreakService>();

        return new DiscordStatsBuilder(builder.Services, builder.BotConfiguration);
    }
}
