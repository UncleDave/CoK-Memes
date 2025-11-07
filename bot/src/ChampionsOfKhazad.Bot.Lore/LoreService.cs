using ChampionsOfKhazad.Bot.Lore.Abstractions;

namespace ChampionsOfKhazad.Bot.Lore;

internal class LoreService(IStoreLore loreStore) : IGetLore, IUpdateLore, ICreateLore, IDeleteLore
{
    public Task<IReadOnlyList<ILore>> GetLoreAsync(CancellationToken cancellationToken = default) => loreStore.ReadLoreAsync(cancellationToken);

    public Task<ILore?> GetLoreAsync(string name, CancellationToken cancellationToken = default) => loreStore.ReadLoreAsync(name, cancellationToken);

    public Task UpdateLoreAsync(IGuildLore guildLore) => loreStore.UpsertLoreAsync(guildLore);

    public Task UpdateLoreAsync(IMemberLore lore) => loreStore.UpsertLoreAsync(lore);

    public Task CreateLoreAsync(IGuildLore lore) => loreStore.UpsertLoreAsync(lore);

    public Task CreateLoreAsync(IMemberLore lore) => loreStore.UpsertLoreAsync(lore);

    public Task DeleteLoreAsync(string name) => loreStore.DeleteLoreAsync(name);
}
