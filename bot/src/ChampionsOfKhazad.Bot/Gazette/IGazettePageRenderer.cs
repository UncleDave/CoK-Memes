using ChampionsOfKhazad.Bot.GenAi;

namespace ChampionsOfKhazad.Bot;

public interface IGazettePageRenderer
{
    GazettePrintEdition Render(GazetteEdition edition, long issueNumber, string dates, byte[]? illustration);
}
