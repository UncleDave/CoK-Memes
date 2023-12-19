using ChampionsOfKhazad.Bot.OpenAi.Embeddings;

namespace ChampionsOfKhazad.Bot.Lore;

internal class RelatedLoreService(IStoreLore loreStore, EmbeddingsService embeddingsService) : IGetRelatedLore
{
    public async Task<IReadOnlyList<Lore>> GetRelatedLoreAsync(string text, uint max = 10)
    {
        var embedding = await embeddingsService.CreateEmbeddingAsync(text);

        return await loreStore.SearchLoreAsync(embedding, max);
    }
}
