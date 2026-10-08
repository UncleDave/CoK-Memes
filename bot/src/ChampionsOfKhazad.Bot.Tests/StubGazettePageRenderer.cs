using ChampionsOfKhazad.Bot.GenAi;

namespace ChampionsOfKhazad.Bot.Tests;

internal sealed class StubGazettePageRenderer : IGazettePageRenderer
{
    public Exception? Failure { get; set; }
    public bool FailWithArtwork { get; set; }

    public GazettePrintEdition Render(GazetteEdition edition, long issueNumber, string dates, byte[]? illustration)
    {
        if (Failure is { } error)
            throw error;
        if (FailWithArtwork && illustration is not null)
            throw new InvalidOperationException("Artwork could not be decoded.");
        return new(
            Enumerable
                .Range(1, edition.Articles.Count > 1 ? 2 : 1)
                .Select(page => new GazettePage($"issue-{issueNumber}-page-{page}.png", [1, 2, 3]))
                .ToArray()
        );
    }
}
