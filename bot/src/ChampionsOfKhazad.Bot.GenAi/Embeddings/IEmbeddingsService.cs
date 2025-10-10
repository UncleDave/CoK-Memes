namespace ChampionsOfKhazad.Bot.GenAi.Embeddings;

public interface IEmbeddingsService
{
    Task<float[]> CreateEmbeddingAsync(string input, CancellationToken cancellationToken = default);
}
