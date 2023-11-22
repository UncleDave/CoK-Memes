using ChampionsOfKhazad.Bot.Core;
using ChampionsOfKhazad.Bot.Lore;
using ChampionsOfKhazad.Bot.OpenAi.Embeddings;
using ChampionsOfKhazad.Bot.Pinecone;

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
            .AddEmbeddingsService(
                options.EmbeddingsApiKey ?? throw new MissingConfigurationValueException(nameof(GuildLoreConfiguration.EmbeddingsApiKey))
            )
            .AddPinecone(
                options.VectorDatabaseApiKey ?? throw new MissingConfigurationValueException(nameof(GuildLoreConfiguration.VectorDatabaseApiKey))
            )
            .AddSingleton<LoreService>()
            .AddSingleton<IGetLore>(sp => sp.GetRequiredService<LoreService>())
            .AddSingleton<IGetRelatedLore>(sp => sp.GetRequiredService<LoreService>())
            .AddSingleton<ICreateLore>(sp => sp.GetRequiredService<LoreService>())
            .AddSingleton<IUpdateLore>(sp => sp.GetRequiredService<LoreService>());

        return new GuildLoreBuilder(builder.Services, builder.BotConfiguration);
    }
}
