using ChampionsOfKhazad.Bot.GenAi;

namespace ChampionsOfKhazad.Bot.Lore;

internal class NoopRelatedLoreService : IGetRelatedLore
{
    public Task<IReadOnlyList<GenAi.Lore>> GetRelatedLoreAsync(string text, uint max = 10) => Task.FromResult<IReadOnlyList<GenAi.Lore>>([]);
}
