namespace ChampionsOfKhazad.Bot.Lore;

internal class NoopRelatedLoreService : IGetRelatedLore
{
    public Task<IReadOnlyList<Lore>> GetRelatedLoreAsync(string text, ushort max = 10) => Task.FromResult<IReadOnlyList<Lore>>([]);
}
