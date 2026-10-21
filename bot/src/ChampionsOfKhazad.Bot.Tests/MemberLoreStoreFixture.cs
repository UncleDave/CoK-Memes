using System.Runtime.CompilerServices;
using ChampionsOfKhazad.Bot.Lore.Abstractions;
using ChampionsOfKhazad.Bot.Lore.Mongo;
using Microsoft.Extensions.AI;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using static ChampionsOfKhazad.Bot.Tests.DiscordConversationFixture;

namespace ChampionsOfKhazad.Bot.Tests;

internal sealed class MemberLoreStoreFixture
{
    [ModuleInitializer]
    internal static void RegisterProductionLoreSerializationConventions() =>
        ConventionRegistry.Register(
            "MemberLorePersistenceTests",
            new ConventionPack { new CamelCaseElementNameConvention(), new IgnoreExtraElementsConvention(true), new IgnoreIfNullConvention(true) },
            type => type == typeof(LoreDocument) || type == typeof(LoreEntrySnapshot) || type == typeof(LoreRevision)
        );

    public IReadOnlyList<LoreDocument> ReadResults { get; set; } = [];
    public FilterDefinition<LoreDocument>? ReadFilter { get; private set; }
    public FindOptions<LoreDocument, LoreDocument>? ReadOptions { get; private set; }
    public CancellationToken ReadToken { get; private set; }
    public LoreDocument? Inserted { get; private set; }
    public LoreDocument? Replacement { get; private set; }
    public Exception? InsertFailure { get; set; }
    public Exception? ReplaceFailure { get; set; }
    public int EmbeddingCalls { get; private set; }
    public MongoLoreStore Store { get; }

    public MemberLoreStoreFixture()
    {
        var collection = Stub<IMongoCollection<LoreDocument>>(
            (method, args) =>
            {
                if (method.Name == "get_DocumentSerializer")
                    return BsonSerializer.SerializerRegistry.GetSerializer<LoreDocument>();
                if (method.Name == "get_Settings")
                    return new MongoCollectionSettings();
                ((CancellationToken)args![^1]!).ThrowIfCancellationRequested();
                if (method.Name == "FindAsync")
                {
                    ReadFilter = (FilterDefinition<LoreDocument>)args[0]!;
                    ReadOptions = (FindOptions<LoreDocument, LoreDocument>)args[1]!;
                    ReadToken = (CancellationToken)args[^1]!;
                    return Task.FromResult<IAsyncCursor<LoreDocument>>(new DocumentCursor(ReadResults));
                }
                if (method.Name == "InsertOneAsync")
                {
                    Inserted = (LoreDocument)args[0]!;
                    return InsertFailure is null ? Task.CompletedTask : Task.FromException(InsertFailure);
                }
                if (method.Name == "ReplaceOneAsync")
                {
                    Replacement = (LoreDocument)args[1]!;
                    return ReplaceFailure is null
                        ? Task.FromResult<ReplaceOneResult>(new ReplaceOneResult.Acknowledged(1, 1, null))
                        : Task.FromException<ReplaceOneResult>(ReplaceFailure);
                }
                throw new NotSupportedException(method.Name);
            }
        );
        Store = new MongoLoreStore(collection, CreateEmbeddings(() => EmbeddingCalls++));
    }

    public static IEmbeddingGenerator<string, Embedding<float>> CreateEmbeddings(Action? onGenerate = null) =>
        Stub<IEmbeddingGenerator<string, Embedding<float>>>(
            (method, args) =>
            {
                if (method.Name == "Dispose")
                    return null;
                if (method.Name != "GenerateAsync")
                    throw new NotSupportedException(method.Name);
                ((CancellationToken)args![^1]!).ThrowIfCancellationRequested();
                onGenerate?.Invoke();
                return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>([new Embedding<float>(new float[] { 0.1f, 0.2f })]));
            }
        );

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
