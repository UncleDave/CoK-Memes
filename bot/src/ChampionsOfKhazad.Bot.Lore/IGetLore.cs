using ChampionsOfKhazad.Bot.Lore.Abstractions;

namespace ChampionsOfKhazad.Bot.Lore;

public interface IGetLore
{
    Task<IReadOnlyList<Abstractions.Lore>> GetLoreAsync(CancellationToken cancellationToken = default);
    Task<Abstractions.Lore?> GetLoreAsync(string name, CancellationToken cancellationToken = default);
}
