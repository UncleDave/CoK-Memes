using ChampionsOfKhazad.Bot.Lore.Abstractions;

namespace ChampionsOfKhazad.Bot.Portal.Tests;

internal sealed class LoreStoreStub : IStoreLore
{
    public bool CreateResult { get; set; } = true;
    public bool UpdateResult { get; set; } = true;
    public Exception? WriteFailure { get; set; }
    public ILore? Created { get; private set; }
    public ILore? Updated { get; private set; }

    public Task<bool> CreateLoreAsync(ILore lore, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Created = lore;
        return WriteFailure is null ? Task.FromResult(CreateResult) : Task.FromException<bool>(WriteFailure);
    }

    public Task<bool> UpdateLoreAsync(ILore lore, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Updated = lore;
        return WriteFailure is null ? Task.FromResult(UpdateResult) : Task.FromException<bool>(WriteFailure);
    }

    public Task<IReadOnlyList<ILore>> ReadLoreAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<ILore?> ReadLoreAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task UpsertLoreAsync(ILore lore) => throw new NotSupportedException();

    public Task UpsertLoreAsync(IGuildLore lore) => throw new NotSupportedException();

    public Task UpsertLoreAsync(IMemberLore lore) => throw new NotSupportedException();

    public Task DeleteLoreAsync(string name) => throw new NotSupportedException();

    public Task<IReadOnlyList<ILore>> SearchLoreAsync(float[] queryVector, uint max, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
