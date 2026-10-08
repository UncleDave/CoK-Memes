using ChampionsOfKhazad.Bot.GenAi;

namespace ChampionsOfKhazad.Bot.Tests;

internal sealed class StubGazettePageRenderer : IGazettePageRenderer
{
    public GazettePrintEdition Render(GazetteEdition edition, long issueNumber, string dates, byte[]? illustration) =>
        new(
            Enumerable
                .Range(1, edition.Articles.Count > 1 ? 2 : 1)
                .Select(page => new GazettePage($"issue-{issueNumber}-page-{page}.png", [1, 2, 3]))
                .ToArray()
        );
}
