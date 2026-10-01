using ChampionsOfKhazad.Bot.Lore.Abstractions;
using Microsoft.Extensions.AI;
using MongoDB.Driver;

namespace ChampionsOfKhazad.Bot.Lore.Mongo;

internal class MongoLoreStore(IMongoCollection<LoreDocument> loreCollection, IEmbeddingGenerator<string, Embedding<float>> embeddingsService)
    : IStoreLore
{
    private readonly ProjectionDefinition<LoreDocument, LoreDocument> _loreProjection = Builders<LoreDocument>.Projection.Exclude(x => x.Embedding);

    public async Task<IReadOnlyList<ILore>> ReadLoreAsync(CancellationToken cancellationToken = default)
    {
        var result = await loreCollection.Find(FilterDefinition<LoreDocument>.Empty).Project(_loreProjection).ToListAsync(cancellationToken);

        return result.Select(x => x.ToModel()).ToList();
    }

    public async Task<ILore?> ReadLoreAsync(string name, CancellationToken cancellationToken = default)
    {
        var result = await loreCollection
            .Find(x => x.Name == name, new FindOptions { Collation = Collections.Lore.UniqueIndex.Collation })
            .Project(_loreProjection)
            .SingleOrDefaultAsync(cancellationToken);

        return result?.ToModel();
    }

    public async Task<bool> CreateLoreAsync(ILore lore, CancellationToken cancellationToken = default)
    {
        var document = await CreateDocumentAsync(lore, cancellationToken);
        try
        {
            // The existing unique, case-insensitive name index arbitrates concurrent creates.
            await loreCollection.InsertOneAsync(document, cancellationToken: cancellationToken);
            return true;
        }
        catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }

    public async Task<bool> UpdateLoreAsync(ILore lore, CancellationToken cancellationToken = default)
    {
        var document = await CreateDocumentAsync(lore, cancellationToken);
        var result = await loreCollection.ReplaceOneAsync(
            x => x.Name == lore.Name,
            document,
            new ReplaceOptions { IsUpsert = false, Collation = Collections.Lore.UniqueIndex.Collation },
            cancellationToken
        );
        return result.MatchedCount == 1;
    }

    public async Task UpsertLoreAsync(ILore lore)
    {
        var document = await CreateDocumentAsync(lore, CancellationToken.None);
        await loreCollection.ReplaceOneAsync(
            x => x.Name == lore.Name,
            document,
            new ReplaceOptions { IsUpsert = true, Collation = Collections.Lore.UniqueIndex.Collation }
        );
    }

    public Task UpsertLoreAsync(IGuildLore lore) => UpsertLoreAsync((ILore)lore);

    public Task UpsertLoreAsync(IMemberLore lore) => UpsertLoreAsync((ILore)lore);

    private async Task<LoreDocument> CreateDocumentAsync(ILore lore, CancellationToken cancellationToken)
    {
        var document = lore switch
        {
            IGuildLore guildLore => new LoreDocument(guildLore),
            IMemberLore memberLore => new LoreDocument(memberLore),
            _ => throw new NotSupportedException($"Lore type '{lore.GetType().FullName}' is not supported."),
        };
        var embeddingResult = await embeddingsService.GenerateAsync(document.Content, cancellationToken: cancellationToken);
        return document with { Embedding = embeddingResult.Vector.ToArray() };
    }

    public async Task<IReadOnlyList<ILore>> SearchLoreAsync(float[] queryVector, uint max, CancellationToken cancellationToken = default)
    {
        var result = await loreCollection.AggregateAsync(
            new EmptyPipelineDefinition<LoreDocument>()
                .VectorSearch("embedding", new QueryVector(queryVector), (int)max, new VectorSearchOptions<LoreDocument> { IndexName = "vector" })
                .Project(_loreProjection),
            cancellationToken: cancellationToken
        );

        var documents = await result.ToListAsync(cancellationToken);

        return documents.Select(x => x.ToModel()).ToList();
    }

    public Task DeleteLoreAsync(string name) =>
        loreCollection.DeleteOneAsync(x => x.Name == name, new DeleteOptions { Collation = Collections.Lore.UniqueIndex.Collation });
}
