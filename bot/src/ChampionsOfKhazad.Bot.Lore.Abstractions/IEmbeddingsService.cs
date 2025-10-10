namespace ChampionsOfKhazad.Bot.Lore.Abstractions;

public interface IEmbeddingsService
{
    Task<float[]> CreateEmbeddingAsync(string input, CancellationToken cancellationToken = default);
}
