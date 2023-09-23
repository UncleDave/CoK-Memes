using ChampionsOfKhazad.Bot.Core;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public class DiscordStatsBuilder : BotBuilder
{
    public DiscordStatsBuilder(IServiceCollection services, BotConfiguration botConfiguration)
        : base(services, botConfiguration) { }
}
