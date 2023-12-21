namespace ChampionsOfKhazad.Bot.Lore;

internal class LoreService(IStoreLore loreStore) : IGetLore, IUpdateLore, ICreateLore
{
    public Task<IReadOnlyList<Lore>> GetLoreAsync(CancellationToken cancellationToken = default) => loreStore.ReadLoreAsync(cancellationToken);

    public Task<Lore?> GetLoreAsync(string name, CancellationToken cancellationToken = default) => loreStore.ReadLoreAsync(name, cancellationToken);

    public Task UpdateLoreAsync(GuildLore guildLore) => loreStore.UpsertLoreAsync(guildLore);

    public Task UpdateLoreAsync(MemberLore lore) => loreStore.UpsertLoreAsync(lore);

    public Task CreateLoreAsync(GuildLore lore) => loreStore.UpsertLoreAsync(lore);

    public Task CreateLoreAsync(MemberLore lore) => loreStore.UpsertLoreAsync(lore);
}
