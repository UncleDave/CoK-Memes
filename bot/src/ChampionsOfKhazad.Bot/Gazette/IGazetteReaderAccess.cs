using Discord;

namespace ChampionsOfKhazad.Bot;

public interface IGazetteReaderAccess
{
    bool CanRead(IComponentInteraction interaction);
}
