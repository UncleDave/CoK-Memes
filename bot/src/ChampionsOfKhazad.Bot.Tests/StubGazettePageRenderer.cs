using ChampionsOfKhazad.Bot.GenAi;

namespace ChampionsOfKhazad.Bot.Tests;

internal sealed class StubGazettePageRenderer : IGazettePageRenderer
{
    public GazettePage Render(GazetteEdition edition, long issueNumber, string dates, byte[]? illustration) =>
        new($"issue-{issueNumber}.png", [1, 2, 3]);
}
