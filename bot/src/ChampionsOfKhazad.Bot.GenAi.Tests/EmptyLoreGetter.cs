using ChampionsOfKhazad.Bot.Lore.Abstractions;

namespace ChampionsOfKhazad.Bot.GenAi.Tests;

internal sealed class EmptyLoreGetter : IGetLore
{
    public Task<IReadOnlyList<ILore>> GetLoreAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ILore>>([]);

    public Task<ILore?> GetLoreAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult<ILore?>(null);

    public Task<IMemberLore?> GetMemberLoreAsync(ulong discordUserId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IMemberLore?>(null);
}
