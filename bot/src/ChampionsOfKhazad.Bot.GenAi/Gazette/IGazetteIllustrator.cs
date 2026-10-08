namespace ChampionsOfKhazad.Bot.GenAi;

public interface IGazetteIllustrator
{
    Task<byte[]> GenerateAsync(string concept, CancellationToken cancellationToken);
}
