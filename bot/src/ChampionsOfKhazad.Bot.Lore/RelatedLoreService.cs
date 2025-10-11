using ChampionsOfKhazad.Bot.Lore.Abstractions;
using Microsoft.Extensions.AI;

namespace ChampionsOfKhazad.Bot.Lore;

internal class RelatedLoreService(IStoreLore loreStore, IEmbeddingGenerator<string, Embedding<float>> embeddingsService) : IGetRelatedLore
{
    public async Task<IReadOnlyList<ILore>> GetRelatedLoreAsync(string text, uint max = 10)
    {
        var embeddingResult = await embeddingsService.GenerateAsync(text);

        return await loreStore.SearchLoreAsync(embeddingResult.Vector.ToArray(), max);
    }
}
