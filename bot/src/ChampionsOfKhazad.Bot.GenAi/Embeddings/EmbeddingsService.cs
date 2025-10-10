using ChampionsOfKhazad.Bot.Lore.Abstractions;
using Microsoft.SemanticKernel.Embeddings;

namespace ChampionsOfKhazad.Bot.GenAi.Embeddings;

internal class EmbeddingsService(ITextEmbeddingGenerationService textEmbeddingService) : IEmbeddingsService
{
    public async Task<float[]> CreateEmbeddingAsync(string input, CancellationToken cancellationToken = default)
    {
        var embedding = await textEmbeddingService.GenerateEmbeddingAsync(input, cancellationToken: cancellationToken);

        return embedding.ToArray();
    }
}
