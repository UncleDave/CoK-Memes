using ChampionsOfKhazad.Bot.Lore.Abstractions;

namespace ChampionsOfKhazad.Bot.Lore;

internal class NoopRelatedLoreService : IGetRelatedLore
{
    public Task<IReadOnlyList<Abstractions.Lore>> GetRelatedLoreAsync(string text, uint max = 10) =>
        Task.FromResult<IReadOnlyList<Abstractions.Lore>>([]);
}
