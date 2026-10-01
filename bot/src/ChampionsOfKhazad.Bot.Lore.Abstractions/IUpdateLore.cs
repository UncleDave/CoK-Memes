namespace ChampionsOfKhazad.Bot.Lore.Abstractions;

public interface IUpdateLore
{
    Task<bool> UpdateLoreAsync(IGuildLore lore, CancellationToken cancellationToken = default);
    Task<bool> UpdateLoreAsync(IMemberLore lore, CancellationToken cancellationToken = default);
}
