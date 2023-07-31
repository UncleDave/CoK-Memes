namespace ChampionsOfKhazad.Bot.Lore;

public interface IGetLore
{
    Task<IReadOnlyList<Lore>> GetLoreAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MemberLore>> GetMemberLoreAsync(CancellationToken cancellationToken = default);
    Task<Lore> GetLoreAsync(string name, CancellationToken cancellationToken = default);
    Task<MemberLore> GetMemberLoreAsync(string name, CancellationToken cancellationToken = default);
}
