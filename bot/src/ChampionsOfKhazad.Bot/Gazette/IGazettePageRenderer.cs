using ChampionsOfKhazad.Bot.GenAi;

namespace ChampionsOfKhazad.Bot;

public interface IGazettePageRenderer
{
    GazettePage Render(GazetteEdition edition, long issueNumber, string dates, byte[]? illustration);
}
