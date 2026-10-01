using ChampionsOfKhazad.Bot.Lore.Abstractions;

namespace ChampionsOfKhazad.Bot.Lore;

internal class LoreService(IStoreLore loreStore) : IGetLore, IUpdateLore, ICreateLore, IDeleteLore
{
    public Task<IReadOnlyList<ILore>> GetLoreAsync(CancellationToken cancellationToken = default) => loreStore.ReadLoreAsync(cancellationToken);

    public Task<ILore?> GetLoreAsync(string name, CancellationToken cancellationToken = default) => loreStore.ReadLoreAsync(name, cancellationToken);

    public Task<bool> UpdateLoreAsync(IGuildLore guildLore, CancellationToken cancellationToken = default) =>
        loreStore.UpdateLoreAsync(guildLore, cancellationToken);

    public Task<bool> UpdateLoreAsync(IMemberLore lore, CancellationToken cancellationToken = default) =>
        loreStore.UpdateLoreAsync(lore, cancellationToken);

    public Task<bool> CreateLoreAsync(IGuildLore lore, CancellationToken cancellationToken = default) =>
        loreStore.CreateLoreAsync(lore, cancellationToken);

    public Task<bool> CreateLoreAsync(IMemberLore lore, CancellationToken cancellationToken = default) =>
        loreStore.CreateLoreAsync(lore, cancellationToken);

    public Task DeleteLoreAsync(string name) => loreStore.DeleteLoreAsync(name);
}
