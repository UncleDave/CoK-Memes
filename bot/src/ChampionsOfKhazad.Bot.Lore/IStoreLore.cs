namespace ChampionsOfKhazad.Bot.Lore;

public interface IStoreLore
{
    Task<IReadOnlyList<Lore>> ReadLoreAsync(CancellationToken cancellationToken = default);
    Task<Lore?> ReadLoreAsync(string name, CancellationToken cancellationToken = default);
    Task UpsertLoreAsync(GuildLore lore, IReadOnlyList<float> embedding);
    Task UpsertLoreAsync(MemberLore lore, IReadOnlyList<float> embedding);
    Task<IReadOnlyList<Lore>> SearchLoreAsync(float[] queryVector, ushort max, CancellationToken cancellationToken = default);
}
