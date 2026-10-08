namespace ChampionsOfKhazad.Bot.Lore.Abstractions;

public interface IEditLoreStore
{
    Task<IReadOnlyList<LoreEditState>> GetEntriesAsync(CancellationToken cancellationToken);
    Task<LoreEditState> GetEntryAsync(string name, CancellationToken cancellationToken);
    Task<bool> TrySaveAsync(LoreEditState expected, LoreRevision revision, CancellationToken cancellationToken);
}
