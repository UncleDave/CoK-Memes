using Microsoft.Extensions.DependencyInjection;

namespace ChampionsOfKhazad.Bot.Lore;

public class GuildLoreBuilder
{
    public IServiceCollection Services { get; }

    internal GuildLoreBuilder(IServiceCollection services)
    {
        Services = services;
    }
}
