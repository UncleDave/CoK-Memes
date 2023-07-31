using ChampionsOfKhazad.Bot.Lore;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static GuildLoreBuilder AddGuildLore(this IServiceCollection services)
    {
        services
            .AddSingleton<LoreService>()
            .AddSingleton<IGetLore>(sp => sp.GetRequiredService<LoreService>())
            .AddSingleton<ICreateLore>(sp => sp.GetRequiredService<LoreService>())
            .AddSingleton<IUpdateLore>(sp => sp.GetRequiredService<LoreService>());

        return new GuildLoreBuilder(services);
    }
}
