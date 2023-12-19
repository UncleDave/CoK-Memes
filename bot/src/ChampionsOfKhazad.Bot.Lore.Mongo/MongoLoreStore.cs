using MongoDB.Driver;

namespace ChampionsOfKhazad.Bot.Lore.Mongo;

internal class MongoLoreStore(IMongoCollection<LoreDocument> loreCollection) : IStoreLore
{
    public async Task<IReadOnlyList<Lore>> ReadLoreAsync(CancellationToken cancellationToken = default)
    {
        var result = await loreCollection.FindAsync(FilterDefinition<LoreDocument>.Empty, cancellationToken: cancellationToken);
        var documents = await result.ToListAsync(cancellationToken);

        return documents.Select(x => x.ToModel()).ToList();
    }

    public async Task<Lore> ReadLoreAsync(string name, CancellationToken cancellationToken = default)
    {
        var result = await loreCollection.FindAsync(x => x.Name == name, cancellationToken: cancellationToken);

        return (await result.SingleOrDefaultAsync(cancellationToken)).ToModel();
    }

    public Task UpsertLoreAsync(GuildLore lore) =>
        loreCollection.ReplaceOneAsync(x => x.Name == lore.Name, new LoreDocument(lore), new ReplaceOptions { IsUpsert = true });

    public Task UpsertLoreAsync(MemberLore lore) =>
        loreCollection.ReplaceOneAsync(x => x.Name == lore.Name, new LoreDocument(lore), new ReplaceOptions { IsUpsert = true });

    public async Task<IReadOnlyList<Lore>> SearchLoreAsync(float[] queryVector, uint max, CancellationToken cancellationToken = default)
    {
        var result = await loreCollection.AggregateAsync(
            new EmptyPipelineDefinition<LoreDocument>().VectorSearch("embedding", new QueryVector(queryVector), (int)max),
            cancellationToken: cancellationToken
        );

        var documents = await result.ToListAsync(cancellationToken);

        return documents.Select(x => x.ToModel()).ToList();
    }
}
