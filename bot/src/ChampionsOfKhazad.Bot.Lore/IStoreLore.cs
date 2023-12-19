namespace ChampionsOfKhazad.Bot.Lore;

public interface IStoreLore
{
    Task<IReadOnlyList<Lore>> ReadLoreAsync(CancellationToken cancellationToken = default);
    Task<Lore> ReadLoreAsync(string name, CancellationToken cancellationToken = default);
    Task UpsertLoreAsync(GuildLore lore);
    Task UpsertLoreAsync(MemberLore lore);
    Task<IReadOnlyList<Lore>> SearchLoreAsync(float[] queryVector, uint max, CancellationToken cancellationToken = default);
}
