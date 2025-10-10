using ChampionsOfKhazad.Bot.Lore.Abstractions;
using MongoDB.Driver;

namespace ChampionsOfKhazad.Bot.Lore.Mongo;

internal class MongoLoreStore(IMongoCollection<LoreDocument> loreCollection) : IStoreLore
{
    public async Task<IReadOnlyList<Abstractions.Lore>> ReadLoreAsync(CancellationToken cancellationToken = default)
    {
        var result = await loreCollection.Find(FilterDefinition<LoreDocument>.Empty).ToListAsync(cancellationToken);

        return result.Select(x => x.ToModel()).ToList();
    }

    public async Task<Abstractions.Lore?> ReadLoreAsync(string name, CancellationToken cancellationToken = default)
    {
        var result = await loreCollection
            .Find(x => x.Name == name, new FindOptions { Collation = Collections.Lore.UniqueIndex.Collation })
            .SingleOrDefaultAsync(cancellationToken);

        return result?.ToModel();
    }

    public Task UpsertLoreAsync(GuildLore lore) =>
        loreCollection.ReplaceOneAsync(
            x => x.Name == lore.Name,
            new LoreDocument(lore),
            new ReplaceOptions { IsUpsert = true, Collation = Collections.Lore.UniqueIndex.Collation }
        );

    public Task UpsertLoreAsync(MemberLore lore) =>
        loreCollection.ReplaceOneAsync(
            x => x.Name == lore.Name,
            new LoreDocument(lore),
            new ReplaceOptions { IsUpsert = true, Collation = Collections.Lore.UniqueIndex.Collation }
        );

    public async Task<IReadOnlyList<Abstractions.Lore>> SearchLoreAsync(float[] queryVector, uint max, CancellationToken cancellationToken = default)
    {
        var result = await loreCollection.AggregateAsync(
            new EmptyPipelineDefinition<LoreDocument>().VectorSearch(
                "embedding",
                new QueryVector(queryVector),
                (int)max,
                new VectorSearchOptions<LoreDocument> { IndexName = "vector" }
            ),
            cancellationToken: cancellationToken
        );

        var documents = await result.ToListAsync(cancellationToken);

        return documents.Select(x => x.ToModel()).ToList();
    }
}
