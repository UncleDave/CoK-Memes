using ChampionsOfKhazad.Bot.HardcoreStats.CharacterDeaths;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class HardcoreStatsBotBuilderExtensions
{
    public static HardcoreStatsBuilder AddHardcoreStats(this BotBuilder botBuilder)
    {
        botBuilder.Services
            .AddMediatR(configuration =>
            {
                configuration.RegisterServicesFromAssemblyContaining<CharacterDeath>();
            })
            .AddSingleton<IRecordCharacterDeaths, CharacterDeathService>();

        return new HardcoreStatsBuilder(botBuilder.Services, botBuilder.BotConfiguration);
    }
}
