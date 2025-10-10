using ChampionsOfKhazad.Bot.Lore;
using ChampionsOfKhazad.Bot.Lore.Abstractions;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class LoreBotBuilderExtensions
{
    public static GuildLoreBuilder AddGuildLore(this BotBuilder builder, Action<GuildLoreConfiguration>? configureGuildLore = null)
    {
        var options = new GuildLoreConfiguration { EmbeddingsApiKey = string.Empty };
        configureGuildLore?.Invoke(options);

        builder
            .Services.AddSingleton<LoreService>()
            .AddSingleton<IGetLore>(sp => sp.GetRequiredService<LoreService>())
            .AddSingleton<ICreateLore>(sp => sp.GetRequiredService<LoreService>())
            .AddSingleton<IUpdateLore>(sp => sp.GetRequiredService<LoreService>())
            .AddSingleton<IGetRelatedLore, RelatedLoreService>();

        return new GuildLoreBuilder(builder.Services, builder.BotConfiguration);
    }
}
