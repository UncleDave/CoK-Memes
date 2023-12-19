using ChampionsOfKhazad.Bot.Lore;
using ChampionsOfKhazad.Bot.OpenAi.Embeddings;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class LoreBotBuilderExtensions
{
    public static GuildLoreBuilder AddGuildLore(this BotBuilder builder, Action<GuildLoreConfiguration> configureGuildLore)
    {
        var options = new GuildLoreConfiguration();
        configureGuildLore(options);

        builder
            .Services
            .AddSingleton<LoreService>()
            .AddSingleton<IGetLore>(sp => sp.GetRequiredService<LoreService>())
            .AddSingleton<ICreateLore>(sp => sp.GetRequiredService<LoreService>())
            .AddSingleton<IUpdateLore>(sp => sp.GetRequiredService<LoreService>());

        if (options.EmbeddingsApiKey is not null)
        {
            builder.Services.AddEmbeddingsService(options.EmbeddingsApiKey).AddSingleton<IGetRelatedLore, RelatedLoreService>();
        }
        else
        {
            builder.Services.AddSingleton<IGetRelatedLore, NoopRelatedLoreService>();
        }

        return new GuildLoreBuilder(builder.Services, builder.BotConfiguration);
    }
}
