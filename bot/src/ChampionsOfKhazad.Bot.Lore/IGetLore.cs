using ChampionsOfKhazad.Bot.GenAi;

namespace ChampionsOfKhazad.Bot.Lore;

public interface IGetLore
{
    Task<IReadOnlyList<GenAi.Lore>> GetLoreAsync(CancellationToken cancellationToken = default);
    Task<GenAi.Lore?> GetLoreAsync(string name, CancellationToken cancellationToken = default);
}
