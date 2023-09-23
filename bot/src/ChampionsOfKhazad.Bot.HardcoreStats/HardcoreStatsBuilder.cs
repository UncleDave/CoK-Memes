using ChampionsOfKhazad.Bot.Core;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public class HardcoreStatsBuilder : BotBuilder
{
    public HardcoreStatsBuilder(IServiceCollection services, BotConfiguration botConfiguration)
        : base(services, botConfiguration) { }
}
