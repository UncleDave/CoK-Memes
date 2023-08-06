using ChampionsOfKhazad.Bot.Lore;
using ChampionsOfKhazad.Bot.OpenAi.Embeddings;
using ChampionsOfKhazad.Bot.Pinecone;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static GuildLoreBuilder AddGuildLore(this IServiceCollection services, GuildLoreOptions options)
    {
        services
            .AddEmbeddingsService(options.EmbeddingsApiKey)
            .AddPinecone(options.VectorDatabaseApiKey)
            .AddSingleton<LoreService>()
            .AddSingleton<IGetLore>(sp => sp.GetRequiredService<LoreService>())
            .AddSingleton<IGetRelatedLore>(sp => sp.GetRequiredService<LoreService>())
            .AddSingleton<ICreateLore>(sp => sp.GetRequiredService<LoreService>())
            .AddSingleton<IUpdateLore>(sp => sp.GetRequiredService<LoreService>());

        return new GuildLoreBuilder(services);
    }
}
