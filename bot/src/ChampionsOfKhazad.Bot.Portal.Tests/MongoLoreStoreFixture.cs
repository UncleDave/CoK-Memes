using ChampionsOfKhazad.Bot.Lore.Mongo;
using Microsoft.Extensions.AI;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace ChampionsOfKhazad.Bot.Portal.Tests;

internal sealed class MongoLoreStoreFixture
{
    // Script responses and record commands; deliberately do not model MongoDB's matching or index behavior.
    public LoreDocument? ReadResult { get; set; }
    public LoreDocument? Inserted { get; private set; }
    public LoreDocument? Replacement { get; private set; }
    public FilterDefinition<LoreDocument>? ReadFilter { get; private set; }
    public FilterDefinition<LoreDocument>? ReplaceFilter { get; private set; }
    public FindOptions<LoreDocument, LoreDocument>? ReadOptions { get; private set; }
    public ReplaceOptions? ReplaceOptions { get; private set; }
    public CancellationToken WriteToken { get; private set; }
    public CancellationToken EmbeddingToken { get; private set; }
    public ReplaceOneResult ReplaceResult { get; set; } = new ReplaceOneResult.Acknowledged(1, 1, null);
    public Exception? EmbeddingFailure { get; set; }
    public Exception? InsertFailure { get; set; }
    public int EmbeddingCalls { get; private set; }
    public MongoLoreStore Store { get; }

    public MongoLoreStoreFixture()
    {
        var collection = DiscordTestProxy.Create<IMongoCollection<LoreDocument>>(
            (method, args) =>
            {
                if (method.Name == "get_DocumentSerializer")
                    return BsonSerializer.SerializerRegistry.GetSerializer<LoreDocument>();
                if (method.Name == "get_Settings")
                    return new MongoCollectionSettings();
                if (method.Name == "FindAsync")
                {
                    var token = (CancellationToken)args![^1]!;
                    token.ThrowIfCancellationRequested();
                    ReadOptions = (FindOptions<LoreDocument, LoreDocument>)args[1]!;
                    ReadFilter = (FilterDefinition<LoreDocument>)args[0]!;
                    return Task.FromResult<IAsyncCursor<LoreDocument>>(new DocumentCursor(ReadResult is null ? [] : [ReadResult]));
                }
                if (method.Name == "InsertOneAsync")
                {
                    WriteToken = (CancellationToken)args![^1]!;
                    WriteToken.ThrowIfCancellationRequested();
                    Inserted = (LoreDocument)args[0]!;
                    return InsertFailure is null ? Task.CompletedTask : Task.FromException(InsertFailure);
                }
                if (method.Name != "ReplaceOneAsync")
                    throw new NotSupportedException(method.Name);
                WriteToken = (CancellationToken)args![^1]!;
                WriteToken.ThrowIfCancellationRequested();
                Replacement = (LoreDocument)args[1]!;
                ReplaceOptions = (ReplaceOptions)args[2]!;
                ReplaceFilter = (FilterDefinition<LoreDocument>)args[0]!;
                return Task.FromResult(ReplaceResult);
            }
        );
        var embeddings = DiscordTestProxy.Create<IEmbeddingGenerator<string, Embedding<float>>>(
            (method, args) =>
            {
                if (method.Name == "Dispose")
                    return null;
                if (method.Name != "GenerateAsync")
                    throw new NotSupportedException(method.Name);
                EmbeddingToken = (CancellationToken)args![^1]!;
                EmbeddingToken.ThrowIfCancellationRequested();
                EmbeddingCalls++;
                return EmbeddingFailure is not null
                    ? Task.FromException<GeneratedEmbeddings<Embedding<float>>>(EmbeddingFailure)
                    : Task.FromResult(new GeneratedEmbeddings<Embedding<float>>([new Embedding<float>(new float[] { 0.1f, 0.2f })]));
            }
        );
        Store = new MongoLoreStore(collection, embeddings);
    }

    private sealed class DocumentCursor(IReadOnlyList<LoreDocument> documents) : IAsyncCursor<LoreDocument>
    {
        private bool _read;
        public IEnumerable<LoreDocument> Current => documents;

        public bool MoveNext(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_read)
                return false;
            _read = true;
            return documents.Count > 0;
        }

        public Task<bool> MoveNextAsync(CancellationToken cancellationToken = default) => Task.FromResult(MoveNext(cancellationToken));

        public void Dispose() { }
    }
}
