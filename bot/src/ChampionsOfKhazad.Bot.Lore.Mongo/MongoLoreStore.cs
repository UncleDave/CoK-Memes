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

    public Task UpsertLoreAsync(ILore lore) =>
        lore switch
        {
            IGuildLore guildLore => UpsertLoreAsync(guildLore),
            IMemberLore memberLore => UpsertLoreAsync(memberLore),
            _ => throw new NotSupportedException($"Lore type '{lore.GetType().FullName}' is not supported."),
        };

    public async Task UpsertLoreAsync(IGuildLore lore)
    {
        var embeddingResult = await embeddingsService.GenerateAsync(lore.Content);
        var document = new LoreDocument(lore) { Embedding = embeddingResult.Vector.ToArray() };

        await loreCollection.ReplaceOneAsync(
            x => x.Name == lore.Name,
            document,
            new ReplaceOptions { IsUpsert = true, Collation = Collections.Lore.UniqueIndex.Collation }
        );
    }

    public async Task UpsertLoreAsync(IMemberLore lore)
    {
        var content = lore.ToString() ?? string.Empty;
        var embeddingResult = await embeddingsService.GenerateAsync(content);
        var document = new LoreDocument(lore) { Embedding = embeddingResult.Vector.ToArray() };

        await loreCollection.ReplaceOneAsync(
            x => x.Name == lore.Name,
            document,
            new ReplaceOptions { IsUpsert = true, Collation = Collections.Lore.UniqueIndex.Collation }
        );
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
}
