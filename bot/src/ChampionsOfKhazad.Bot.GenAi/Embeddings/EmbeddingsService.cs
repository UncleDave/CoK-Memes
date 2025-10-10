using Microsoft.SemanticKernel.Embeddings;

namespace ChampionsOfKhazad.Bot.GenAi.Embeddings;

#pragma warning disable CS0618 // Type or member is obsolete
public class EmbeddingsService(ITextEmbeddingGenerationService textEmbeddingService) : IEmbeddingsService
#pragma warning restore CS0618 // Type or member is obsolete
{
    public async Task<float[]> CreateEmbeddingAsync(string input, CancellationToken cancellationToken = default)
    {
        var embedding = await textEmbeddingService.GenerateEmbeddingAsync(input, cancellationToken: cancellationToken);

        return embedding.ToArray();
    }
}
