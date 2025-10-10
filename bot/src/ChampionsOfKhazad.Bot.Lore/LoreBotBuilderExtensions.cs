using ChampionsOfKhazad.Bot.GenAi.Embeddings;
using ChampionsOfKhazad.Bot.Lore;
using ChampionsOfKhazad.Bot.Lore.Abstractions;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class LoreBotBuilderExtensions
{
    public static GuildLoreBuilder AddGuildLore(this BotBuilder builder, Action<GuildLoreConfiguration>? configureGuildLore = null)
    {
        var options = new GuildLoreConfiguration();
        configureGuildLore?.Invoke(options);

        builder
            .Services.AddSingleton<LoreService>()
            .AddSingleton<IGetLore>(sp => sp.GetRequiredService<LoreService>())
            .AddSingleton<ICreateLore>(sp => sp.GetRequiredService<LoreService>())
            .AddSingleton<IUpdateLore>(sp => sp.GetRequiredService<LoreService>());

        if (options.EmbeddingsApiKey is not null)
        {
            builder.Services.AddSingleton<IGetRelatedLore, RelatedLoreService>();
        }
        else
        {
            builder.Services.AddSingleton<IGetRelatedLore, NoopRelatedLoreService>();
        }

        return new GuildLoreBuilder(builder.Services, builder.BotConfiguration);
    }
}
