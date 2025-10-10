namespace ChampionsOfKhazad.Bot.Lore.Abstractions;

public interface IStoreLore
{
    Task<IReadOnlyList<ILore>> ReadLoreAsync(CancellationToken cancellationToken = default);
    Task<ILore?> ReadLoreAsync(string name, CancellationToken cancellationToken = default);
    Task UpsertLoreAsync(IGuildLore lore);
    Task UpsertLoreAsync(IMemberLore lore);
    Task<IReadOnlyList<ILore>> SearchLoreAsync(float[] queryVector, uint max, CancellationToken cancellationToken = default);
}
