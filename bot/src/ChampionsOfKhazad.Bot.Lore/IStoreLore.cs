using ChampionsOfKhazad.Bot.Lore.Abstractions;

namespace ChampionsOfKhazad.Bot.Lore;

public interface IStoreLore
{
    Task<IReadOnlyList<Abstractions.Lore>> ReadLoreAsync(CancellationToken cancellationToken = default);
    Task<Abstractions.Lore?> ReadLoreAsync(string name, CancellationToken cancellationToken = default);
    Task UpsertLoreAsync(GuildLore lore);
    Task UpsertLoreAsync(MemberLore lore);
    Task<IReadOnlyList<Abstractions.Lore>> SearchLoreAsync(float[] queryVector, uint max, CancellationToken cancellationToken = default);
}
