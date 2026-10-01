namespace ChampionsOfKhazad.Bot.Lore.Abstractions;

public interface ICreateLore
{
    Task<bool> CreateLoreAsync(IGuildLore lore, CancellationToken cancellationToken = default);
    Task<bool> CreateLoreAsync(IMemberLore lore, CancellationToken cancellationToken = default);
}
