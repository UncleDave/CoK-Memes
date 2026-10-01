using System.Reflection;
using ChampionsOfKhazad.Bot.Lore;
using ChampionsOfKhazad.Bot.Lore.Abstractions;
using ChampionsOfKhazad.Bot.Lore.Mongo;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace ChampionsOfKhazad.Bot.Tests;

public class MongoLoreStoreTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreatingLoreInsertsRatherThanReplacingAnExistingEntry(bool member)
    {
        var fixture = new StoreFixture();
        using var services = fixture.CreateServices();
        var creator = services.GetRequiredService<ICreateLore>();
        var token = TestContext.Current.CancellationToken;
        var created = member
            ? await creator.CreateLoreAsync(new MemberLore("Member", "they", "UK", "Character", "Biography"), token)
            : await creator.CreateLoreAsync(new GuildLore("Guild", "Content"), token);

        Assert.True(created);
        Assert.NotNull(fixture.Inserted);
        Assert.Null(fixture.Replacement);
        Assert.Equal(token, fixture.WriteToken);
        Assert.Equal(token, fixture.EmbeddingToken);
        Assert.NotNull(fixture.Inserted.Embedding);
        Assert.Equal([0.1f, 0.2f], fixture.Inserted.Embedding);
    }

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(1, 0, true)]
    [InlineData(1, 1, true)]
    public async Task UpdatingLoreRequiresAnExistingMatchWithoutUpserting(long matched, long modified, bool expected)
    {
        var fixture = new StoreFixture { ReplaceResult = new ReplaceOneResult.Acknowledged(matched, modified, null) };
        using var services = fixture.CreateServices();
        var updated = await services
            .GetRequiredService<IUpdateLore>()
            .UpdateLoreAsync(new GuildLore("Guild", "Content"), TestContext.Current.CancellationToken);

        Assert.Equal(expected, updated);
        Assert.Null(fixture.Inserted);
        Assert.NotNull(fixture.Replacement);
        Assert.False(fixture.ReplaceOptions!.IsUpsert);
        Assert.Equal(Collections.Lore.UniqueIndex.Collation, fixture.ReplaceOptions.Collation);
        Assert.Equal(TestContext.Current.CancellationToken, fixture.WriteToken);
    }

    [Fact]
    public async Task EmbeddingRegenerationStillSupportsExplicitUpserts()
    {
        var fixture = new StoreFixture();

        await fixture.Store.UpsertLoreAsync(new GuildLore("Guild", "Content"));

        Assert.NotNull(fixture.Replacement);
        Assert.True(fixture.ReplaceOptions!.IsUpsert);
    }

    [Fact]
    public async Task CancelledEmbeddingDoesNotWriteLore()
    {
        var fixture = new StoreFixture();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.Store.CreateLoreAsync(new GuildLore("Guild", "Content"), cancellation.Token)
        );

        Assert.Null(fixture.Inserted);
        Assert.Null(fixture.Replacement);
    }

    private sealed class StoreFixture
    {
        public LoreDocument? Inserted { get; private set; }
        public LoreDocument? Replacement { get; private set; }
        public ReplaceOptions? ReplaceOptions { get; private set; }
        public CancellationToken WriteToken { get; private set; }
        public CancellationToken EmbeddingToken { get; private set; }
        public ReplaceOneResult ReplaceResult { get; set; } = new ReplaceOneResult.Acknowledged(1, 1, null);
        public MongoLoreStore Store { get; }

        public StoreFixture()
        {
            var collection = Stub<IMongoCollection<LoreDocument>>(
                (method, args) =>
                {
                    if (method.Name == "InsertOneAsync")
                    {
                        Inserted = (LoreDocument)args![0]!;
                        WriteToken = (CancellationToken)args[^1]!;
                        return Task.CompletedTask;
                    }
                    if (method.Name != "ReplaceOneAsync")
                        throw new NotSupportedException(method.Name);
                    Replacement = (LoreDocument)args![1]!;
                    ReplaceOptions = (ReplaceOptions)args[2]!;
                    WriteToken = (CancellationToken)args[^1]!;
                    return Task.FromResult(ReplaceResult);
                }
            );
            var embeddings = Stub<IEmbeddingGenerator<string, Embedding<float>>>(
                (method, args) =>
                {
                    if (method.Name != "GenerateAsync")
                        throw new NotSupportedException(method.Name);
                    EmbeddingToken = (CancellationToken)args![^1]!;
                    EmbeddingToken.ThrowIfCancellationRequested();
                    return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>([new Embedding<float>(new float[] { 0.1f, 0.2f })]));
                }
            );
            Store = new MongoLoreStore(collection, embeddings);
        }

        public ServiceProvider CreateServices()
        {
            var services = new ServiceCollection();
            services.AddBot(_ => { }).AddGuildLore();
            services.AddSingleton<IStoreLore>(Store);
            return services.BuildServiceProvider();
        }
    }

    private static T Stub<T>(Func<MethodInfo, object?[]?, object?> invoke)
        where T : class
    {
        var proxy = DispatchProxy.Create<T, DiscordTestProxy>();
        ((DiscordTestProxy)(object)proxy).InvokeMethod = invoke;
        return proxy;
    }
}
