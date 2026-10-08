using ChampionsOfKhazad.Bot.GenAi;

namespace ChampionsOfKhazad.Bot.Tests;

internal sealed class StubGazetteIllustrator : IGazetteIllustrator
{
    public int Calls { get; private set; }
    public bool Fails { get; set; }

    public Task<byte[]> GenerateAsync(string concept, CancellationToken cancellationToken)
    {
        Calls++;
        return Fails ? Task.FromException<byte[]>(new InvalidOperationException("Artwork unavailable")) : Task.FromResult<byte[]>([1, 2, 3]);
    }
}
