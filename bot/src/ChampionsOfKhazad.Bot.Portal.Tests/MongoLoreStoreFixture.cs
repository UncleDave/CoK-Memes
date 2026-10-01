using System.Net;
using System.Reflection;
using ChampionsOfKhazad.Bot.Lore.Abstractions;
using ChampionsOfKhazad.Bot.Lore.Mongo;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MongoDB.Driver.Core.Clusters;
using MongoDB.Driver.Core.Connections;
using MongoDB.Driver.Core.Servers;

namespace ChampionsOfKhazad.Bot.Portal.Tests;

internal sealed class MongoLoreStoreFixture
{
    private readonly Lock _lock = new();
    private readonly List<LoreDocument> _documents = [];
    private int _embeddingCalls;
    private int _duplicateKeyFailures;
    public LoreDocument? Inserted { get; private set; }
    public LoreDocument? Replacement { get; private set; }
    public FindOptions<LoreDocument, LoreDocument>? ReadOptions { get; private set; }
    public ReplaceOptions? ReplaceOptions { get; private set; }
    public CancellationToken WriteToken { get; private set; }
    public CancellationToken EmbeddingToken { get; private set; }
    public ReplaceOneResult? ReplaceResult { get; set; }
    public Exception? EmbeddingFailure { get; set; }
    public Exception? InsertFailure { get; set; }
    public Func<Task>? BeforeInsert { get; set; }
    public int EmbeddingCalls => _embeddingCalls;
    public int DuplicateKeyFailures => _duplicateKeyFailures;
    public MongoLoreStore Store { get; }
    public IEmbeddingGenerator<string, Embedding<float>> Embeddings { get; }
    public IReadOnlyList<LoreDocument> Documents
    {
        get
        {
            lock (_lock)
                return _documents.ToArray();
        }
    }

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
                    var name = GetFilterName((FilterDefinition<LoreDocument>)args[0]!);
                    var match = Documents.Where(document => NamesMatch(document.Name, name, ReadOptions.Collation)).ToArray();
                    return Task.FromResult<IAsyncCursor<LoreDocument>>(new DocumentCursor(match));
                }
                if (method.Name == "InsertOneAsync")
                    return InsertAsync((LoreDocument)args![0]!, (CancellationToken)args[^1]!);
                if (method.Name != "ReplaceOneAsync")
                    throw new NotSupportedException(method.Name);
                WriteToken = (CancellationToken)args![^1]!;
                WriteToken.ThrowIfCancellationRequested();
                Replacement = (LoreDocument)args[1]!;
                ReplaceOptions = (ReplaceOptions)args[2]!;
                var filterName = GetFilterName((FilterDefinition<LoreDocument>)args[0]!);
                lock (_lock)
                {
                    var index = _documents.FindIndex(document => NamesMatch(document.Name, filterName, ReplaceOptions.Collation));
                    var result = ReplaceResult ?? new ReplaceOneResult.Acknowledged(index >= 0 ? 1 : 0, index >= 0 ? 1 : 0, null);
                    if (index >= 0 && result.MatchedCount == 1)
                        _documents[index] = Replacement;
                    else if (index < 0 && ReplaceOptions.IsUpsert)
                        _documents.Add(Replacement);
                    return Task.FromResult(result);
                }
            }
        );
        Embeddings = DiscordTestProxy.Create<IEmbeddingGenerator<string, Embedding<float>>>(
            (method, args) =>
            {
                if (method.Name == "Dispose")
                    return null;
                if (method.Name != "GenerateAsync")
                    throw new NotSupportedException(method.Name);
                EmbeddingToken = (CancellationToken)args![^1]!;
                EmbeddingToken.ThrowIfCancellationRequested();
                Interlocked.Increment(ref _embeddingCalls);
                return EmbeddingFailure is not null
                    ? Task.FromException<GeneratedEmbeddings<Embedding<float>>>(EmbeddingFailure)
                    : Task.FromResult(new GeneratedEmbeddings<Embedding<float>>([new Embedding<float>(new float[] { 0.1f, 0.2f })]));
            }
        );
        Store = new MongoLoreStore(collection, Embeddings);
    }

    public void Seed(LoreDocument document)
    {
        lock (_lock)
            _documents.Add(document);
    }

    public ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddBot(_ => { }).AddGuildLore();
        services.AddSingleton<IStoreLore>(Store);
        return services.BuildServiceProvider();
    }

    private async Task InsertAsync(LoreDocument document, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        WriteToken = token;
        if (BeforeInsert is not null)
            await BeforeInsert();
        if (InsertFailure is not null)
            throw InsertFailure;
        lock (_lock)
        {
            // Model the collection's existing case-insensitive unique name index, not the store's preflight read.
            if (_documents.Any(existing => NamesMatch(existing.Name, document.Name, Collections.Lore.UniqueIndex.Collation)))
            {
                Interlocked.Increment(ref _duplicateKeyFailures);
                throw DuplicateNameException();
            }
            Inserted = document;
            _documents.Add(document);
        }
    }

    private static string GetFilterName(FilterDefinition<LoreDocument> filter) =>
        filter
            .Render(new RenderArgs<LoreDocument>(BsonSerializer.SerializerRegistry.GetSerializer<LoreDocument>(), BsonSerializer.SerializerRegistry))
            .GetElement(0)
            .Value.AsString;

    private static bool NamesMatch(string first, string second, Collation? collation) =>
        string.Equals(
            first,
            second,
            collation?.Strength == CollationStrength.Primary ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal
        );

    private static MongoWriteException DuplicateNameException()
    {
        // The driver exposes WriteError but its constructor is internal; construct the real duplicate-key exception for the catch-path test.
        var error = (WriteError)
            Activator.CreateInstance(
                typeof(WriteError),
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                [ServerErrorCategory.DuplicateKey, 11000, "Duplicate lore name", new BsonDocument()],
                null
            )!;
        return new MongoWriteException(new ConnectionId(new ServerId(new ClusterId(), new DnsEndPoint("localhost", 27017))), error, null, null);
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
