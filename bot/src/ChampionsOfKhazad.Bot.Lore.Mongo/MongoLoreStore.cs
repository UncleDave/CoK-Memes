using ChampionsOfKhazad.Bot.Lore.Abstractions;
using Microsoft.SemanticKernel.Embeddings;
using MongoDB.Driver;

namespace ChampionsOfKhazad.Bot.Lore.Mongo;

internal class MongoLoreStore(IMongoCollection<LoreDocument> loreCollection, ITextEmbeddingGenerationService embeddingsService) : IStoreLore
{
    public async Task<IReadOnlyList<ILore>> ReadLoreAsync(CancellationToken cancellationToken = default)
    {
        var result = await loreCollection.Find(FilterDefinition<LoreDocument>.Empty).ToListAsync(cancellationToken);

        return result.Select(x => x.ToModel()).ToList();
    }

    public async Task<ILore?> ReadLoreAsync(string name, CancellationToken cancellationToken = default)
    {
        var result = await loreCollection
            .Find(x => x.Name == name, new FindOptions { Collation = Collections.Lore.UniqueIndex.Collation })
            .SingleOrDefaultAsync(cancellationToken);

        return result?.ToModel();
    }

    public async Task UpsertLoreAsync(IGuildLore lore)
    {
        var embeddingVector = await embeddingsService.GenerateEmbeddingAsync(lore.Content);
        var document = new LoreDocument(lore) { Embedding = embeddingVector.ToArray() };

        await loreCollection.ReplaceOneAsync(
            x => x.Name == lore.Name,
            document,
            new ReplaceOptions { IsUpsert = true, Collation = Collections.Lore.UniqueIndex.Collation }
        );
    }

    public async Task UpsertLoreAsync(IMemberLore lore)
    {
        var content = lore.ToString() ?? string.Empty;
        var embeddingVector = await embeddingsService.GenerateEmbeddingAsync(content);
        var document = new LoreDocument(lore) { Embedding = embeddingVector.ToArray() };

        await loreCollection.ReplaceOneAsync(
            x => x.Name == lore.Name,
            document,
            new ReplaceOptions { IsUpsert = true, Collation = Collections.Lore.UniqueIndex.Collation }
        );
    }

    public async Task<IReadOnlyList<ILore>> SearchLoreAsync(float[] queryVector, uint max, CancellationToken cancellationToken = default)
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
