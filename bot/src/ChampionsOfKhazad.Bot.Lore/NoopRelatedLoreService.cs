namespace ChampionsOfKhazad.Bot.Lore;

internal class NoopRelatedLoreService : IGetRelatedLore
{
    public Task<IReadOnlyList<Lore>> GetRelatedLoreAsync(string text, uint max = 10) => Task.FromResult<IReadOnlyList<Lore>>(Array.Empty<Lore>());
}
