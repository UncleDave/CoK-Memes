using ChampionsOfKhazad.Bot.Core;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public class GuildLoreBuilder : BotBuilder
{
    public GuildLoreBuilder(IServiceCollection services, BotConfiguration botConfiguration)
        : base(services, botConfiguration) { }
}
