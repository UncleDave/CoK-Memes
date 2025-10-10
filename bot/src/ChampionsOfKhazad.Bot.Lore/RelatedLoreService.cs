using ChampionsOfKhazad.Bot.Lore.Abstractions;
using Microsoft.SemanticKernel.Embeddings;

namespace ChampionsOfKhazad.Bot.Lore;

internal class RelatedLoreService(IStoreLore loreStore, ITextEmbeddingGenerationService embeddingsService) : IGetRelatedLore
{
    public async Task<IReadOnlyList<ILore>> GetRelatedLoreAsync(string text, uint max = 10)
    {
        var embeddingVector = await embeddingsService.GenerateEmbeddingAsync(text);

        return await loreStore.SearchLoreAsync(embeddingVector.ToArray(), max);
    }
}
