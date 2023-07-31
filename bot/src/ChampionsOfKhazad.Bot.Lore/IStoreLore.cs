namespace ChampionsOfKhazad.Bot.Lore;

public interface IStoreLore
{
    Task<IReadOnlyList<Lore>> ReadLoreAsync(CancellationToken cancellationToken = default);
    Task<Lore> ReadLoreAsync(string name, CancellationToken cancellationToken = default);
    Task UpsertLoreAsync(params Lore[] lore);
    Task<IReadOnlyList<MemberLore>> ReadMemberLoreAsync(CancellationToken cancellationToken = default);
    Task<MemberLore> ReadMemberLoreAsync(string name, CancellationToken cancellationToken = default);
    Task UpsertMemberLoreAsync(params MemberLore[] lore);
}
