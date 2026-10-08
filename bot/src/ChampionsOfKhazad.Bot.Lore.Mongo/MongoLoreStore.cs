using ChampionsOfKhazad.Bot.Lore.Abstractions;
using Microsoft.Extensions.AI;
using MongoDB.Driver;

namespace ChampionsOfKhazad.Bot.Lore.Mongo;

internal class MongoLoreStore(IMongoCollection<LoreDocument> loreCollection, IEmbeddingGenerator<string, Embedding<float>> embeddingsService)
    : IStoreLore,
        IEditLoreStore
{
    public const int MaximumHistory = 20;
    private static readonly FilterDefinition<LoreDocument> Active = Builders<LoreDocument>.Filter.Ne(x => x.Deleted, true);
    private readonly ProjectionDefinition<LoreDocument, LoreDocument> _loreProjection = Builders<LoreDocument>
        .Projection.Exclude(x => x.Embedding)
        .Exclude(x => x.History);

    public async Task<IReadOnlyList<ILore>> ReadLoreAsync(CancellationToken cancellationToken = default)
    {
        var result = await loreCollection.Find(Active).Project(_loreProjection).ToListAsync(cancellationToken);

        return result.Select(x => x.ToModel()).ToList();
    }

    public async Task<ILore?> ReadLoreAsync(string name, CancellationToken cancellationToken = default)
    {
        var result = await loreCollection
            .Find(
                Builders<LoreDocument>.Filter.Eq(x => x.Name, name) & Active,
                new FindOptions { Collation = Collections.Lore.UniqueIndex.Collation }
            )
            .Project(_loreProjection)
            .SingleOrDefaultAsync(cancellationToken);

        return result?.ToModel();
    }

    public async Task<bool> CreateLoreAsync(ILore lore, CancellationToken cancellationToken = default)
    {
        var state = await GetEntryAsync(lore.Name, cancellationToken);
        if (state.Entry is not null)
            return false;
        return await TrySaveAsync(state, RevisionFor(state, LoreEntrySnapshot.FromLore(lore) with { Name = state.Name }), cancellationToken);
    }

    public async Task<bool> UpdateLoreAsync(ILore lore, CancellationToken cancellationToken = default)
    {
        var state = await GetEntryAsync(lore.Name, cancellationToken);
        if (state.Entry is null)
            return false;
        return await TrySaveAsync(state, RevisionFor(state, LoreEntrySnapshot.FromLore(lore) with { Name = state.Name }), cancellationToken);
    }

    public async Task UpsertLoreAsync(ILore lore)
    {
        var state = await GetEntryAsync(lore.Name, CancellationToken.None);
        if (!await TrySaveAsync(state, RevisionFor(state, LoreEntrySnapshot.FromLore(lore) with { Name = state.Name }), CancellationToken.None))
            throw new InvalidOperationException("Lore changed during the upsert; no overwrite was made.");
    }

    public Task UpsertLoreAsync(IGuildLore lore) => UpsertLoreAsync((ILore)lore);

    public Task UpsertLoreAsync(IMemberLore lore) => UpsertLoreAsync((ILore)lore);

    public async Task<IReadOnlyList<LoreEditState>> GetEntriesAsync(CancellationToken cancellationToken)
    {
        var documents = await loreCollection.Find(Active).Project(_loreProjection).ToListAsync(cancellationToken);
        return documents.Select(document => document.ToEditState()).ToArray();
    }

    public async Task<LoreEditState> GetEntryAsync(string name, CancellationToken cancellationToken)
    {
        ProjectionDefinition<LoreDocument, LoreDocument> projection = Builders<LoreDocument>.Projection.Exclude(x => x.Embedding);
        var document = await loreCollection
            .Find(x => x.Name == name, new FindOptions { Collation = Collections.Lore.UniqueIndex.Collation })
            .Project(projection)
            .SingleOrDefaultAsync(cancellationToken);
        return document?.ToEditState() ?? new(name, false, null, null, []);
    }

    public async Task<bool> TrySaveAsync(LoreEditState expected, LoreRevision revision, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (revision.After is { } next && next.Name != expected.Name)
            throw new InvalidOperationException("Lore edits cannot rename entries.");
        var document = revision.After is { } entry ? LoreDocument.FromSnapshot(entry) : new LoreDocument(expected.Name, "") { Deleted = true };
        if (!document.Deleted)
        {
            var embedding = await embeddingsService.GenerateAsync(document.Content, cancellationToken: cancellationToken);
            document = document with { Embedding = embedding.Vector.ToArray() };
        }
        document = document with { Revision = revision.Id, History = expected.History.Append(revision).TakeLast(MaximumHistory).ToArray() };
        cancellationToken.ThrowIfCancellationRequested();
        if (!expected.Exists)
        {
            try
            {
                await loreCollection.InsertOneAsync(document, cancellationToken: cancellationToken);
                return true;
            }
            catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
            {
                return false;
            }
        }
        var filter =
            Builders<LoreDocument>.Filter.Eq(x => x.Name, expected.Name) & Builders<LoreDocument>.Filter.Eq(x => x.Revision, expected.Revision);
        var result = await loreCollection.ReplaceOneAsync(
            filter,
            document,
            new ReplaceOptions { IsUpsert = false, Collation = Collections.Lore.UniqueIndex.Collation },
            cancellationToken
        );
        return result.MatchedCount == 1;
    }

    private static LoreRevision RevisionFor(LoreEditState state, LoreEntrySnapshot? next) =>
        new(Guid.NewGuid().ToString("N"), DateTime.UtcNow, "lore-store", null, next is null ? "delete" : "save", state.Entry, next);

    public async Task<IReadOnlyList<ILore>> SearchLoreAsync(float[] queryVector, uint max, CancellationToken cancellationToken = default)
    {
        var result = await loreCollection.AggregateAsync(
            new EmptyPipelineDefinition<LoreDocument>()
                .VectorSearch("embedding", new QueryVector(queryVector), (int)max, new VectorSearchOptions<LoreDocument> { IndexName = "vector" })
                .Match(Active)
                .Project(_loreProjection),
            cancellationToken: cancellationToken
        );

        var documents = await result.ToListAsync(cancellationToken);

        return documents.Select(x => x.ToModel()).ToList();
    }

    public async Task DeleteLoreAsync(string name)
    {
        var state = await GetEntryAsync(name, CancellationToken.None);
        if (state.Entry is not null && !await TrySaveAsync(state, RevisionFor(state, null), CancellationToken.None))
            throw new InvalidOperationException("Lore changed during deletion; no deletion was made.");
    }
}
