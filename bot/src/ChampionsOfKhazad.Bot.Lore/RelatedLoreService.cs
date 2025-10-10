using ChampionsOfKhazad.Bot.GenAi.Embeddings;
using ChampionsOfKhazad.Bot.Lore.Abstractions;

namespace ChampionsOfKhazad.Bot.Lore;

internal class RelatedLoreService(IStoreLore loreStore, IEmbeddingsService embeddingsService) : IGetRelatedLore
{
    public async Task<IReadOnlyList<ILore>> GetRelatedLoreAsync(string text, uint max = 10)
    {
        var embedding = await embeddingsService.CreateEmbeddingAsync(text);

        return await loreStore.SearchLoreAsync(embedding, max);
    }
}
