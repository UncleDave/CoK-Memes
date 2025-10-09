using ChampionsOfKhazad.Bot.GenAi;

namespace ChampionsOfKhazad.Bot.Lore;

public interface IStoreLore
{
    Task<IReadOnlyList<GenAi.Lore>> ReadLoreAsync(CancellationToken cancellationToken = default);
    Task<GenAi.Lore?> ReadLoreAsync(string name, CancellationToken cancellationToken = default);
    Task UpsertLoreAsync(GuildLore lore);
    Task UpsertLoreAsync(MemberLore lore);
    Task<IReadOnlyList<GenAi.Lore>> SearchLoreAsync(float[] queryVector, uint max, CancellationToken cancellationToken = default);
}
