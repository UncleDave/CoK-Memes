namespace ChampionsOfKhazad.Bot.Lore.Abstractions;

public interface IGetLore
{
    Task<IReadOnlyList<ILore>> GetLoreAsync(CancellationToken cancellationToken = default);
    Task<ILore?> GetLoreAsync(string name, CancellationToken cancellationToken = default);
}
