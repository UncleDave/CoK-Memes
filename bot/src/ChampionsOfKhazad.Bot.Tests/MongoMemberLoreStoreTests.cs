using System.Net;
using System.Reflection;
using ChampionsOfKhazad.Bot.Lore;
using ChampionsOfKhazad.Bot.Lore.Abstractions;
using ChampionsOfKhazad.Bot.Lore.Mongo;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MongoDB.Driver.Core.Clusters;
using MongoDB.Driver.Core.Connections;
using MongoDB.Driver.Core.Servers;
using static ChampionsOfKhazad.Bot.Tests.DiscordConversationFixture;

namespace ChampionsOfKhazad.Bot.Tests;

public class MongoMemberLoreStoreTests
{
    private const ulong DiscordUserId = 1234567890123456789;

    [Theory]
    [InlineData(1UL)]
    [InlineData(DiscordUserId)]
    [InlineData((ulong)long.MaxValue)]
    public async Task IdentityLookupUsesOnlyAnExactAccountIdAndActiveMemberFilterWithADuplicateBound(ulong discordUserId)
    {
        var member = Member("Grim", discordUserId);
        var fixture = new MemberLoreStoreFixture { ReadResults = [new LoreDocument(member)] };

        Assert.Equal(member, await fixture.Store.ReadMemberLoreAsync(discordUserId, TestContext.Current.CancellationToken));

        var filter = Render(fixture.ReadFilter!);
        Assert.Equal((long)discordUserId, filter[Field(nameof(LoreDocument.DiscordUserId))].AsInt64);
        Assert.True(filter[Field(nameof(LoreDocument.Deleted))]["$ne"].AsBoolean);
        Assert.Equal(BsonNull.Value, filter[Field(nameof(LoreDocument.MainCharacter))]["$ne"]);
        Assert.False(filter.Contains(Field(nameof(LoreDocument.Name))));
        Assert.Equal(2, fixture.ReadOptions!.Limit);
        Assert.Null(fixture.ReadOptions.Collation);
        Assert.Equal(TestContext.Current.CancellationToken, fixture.ReadToken);
        var projection = fixture.ReadOptions.Projection!.Render(RenderArgs()).Document;
        Assert.Equal(0, projection[Field(nameof(LoreDocument.Embedding))].AsInt32);
        Assert.Equal(0, projection[Field(nameof(LoreDocument.History))].AsInt32);
        Assert.False(projection.Contains(Field(nameof(LoreDocument.DiscordUserId))));
    }

    [Fact]
    public async Task MissingBindingsReturnNullAndDuplicateBindingsFailClosedInsteadOfChoosingAName()
    {
        var fixture = new MemberLoreStoreFixture();
        Assert.Null(await fixture.Store.ReadMemberLoreAsync(DiscordUserId, TestContext.Current.CancellationToken));
        fixture.ReadResults = [new LoreDocument(Member("First", DiscordUserId)), new LoreDocument(Member("Second", DiscordUserId))];

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Store.ReadMemberLoreAsync(DiscordUserId, TestContext.Current.CancellationToken)
        );
        Assert.Null(fixture.Inserted);
        Assert.Null(fixture.Replacement);
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(DiscordUserId)]
    [InlineData((ulong)long.MaxValue)]
    public async Task LoreServiceForwardsSupportedIdentityAndCancellationToPersistence(ulong discordUserId)
    {
        var fixture = new MemberLoreStoreFixture { ReadResults = [new LoreDocument(Member("Grim", discordUserId))] };
        var services = new ServiceCollection();
        services.AddBot(_ => { }).AddGuildLore();
        services.AddSingleton<IStoreLore>(fixture.Store);
        await using var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IGetLore>();

        Assert.Equal(discordUserId, (await service.GetMemberLoreAsync(discordUserId, TestContext.Current.CancellationToken))!.DiscordUserId);
        Assert.Equal(TestContext.Current.CancellationToken, fixture.ReadToken);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetMemberLoreAsync(discordUserId, cancellation.Token));
    }

    [Theory]
    [InlineData(0UL)]
    [InlineData((ulong)long.MaxValue + 1)]
    [InlineData(ulong.MaxValue)]
    public async Task LoreServiceRejectsUnsupportedIdentityBeforeCallingPersistence(ulong discordUserId)
    {
        var services = new ServiceCollection();
        services.AddBot(_ => { }).AddGuildLore();
        services.AddSingleton(Stub<IStoreLore>((_, _) => throw new InvalidOperationException("Persistence must not be called")));
        await using var provider = services.BuildServiceProvider();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            provider.GetRequiredService<IGetLore>().GetMemberLoreAsync(discordUserId, TestContext.Current.CancellationToken)
        );
    }

    public static IEnumerable<object[]> UnsupportedBindings =>
        from operation in new[] { "create", "update", "upsert", "upsert-ilore", "lookup", "try-create", "try-update" }
        from discordUserId in new[] { 0UL, (ulong)long.MaxValue + 1, ulong.MaxValue }
        select new object[] { operation, discordUserId };

    [Theory]
    [MemberData(nameof(UnsupportedBindings))]
    public async Task UnsupportedBindingsAreRejectedBeforeAnyReadEmbeddingOrWrite(string operation, ulong discordUserId)
    {
        var fixture = new MemberLoreStoreFixture();
        var invalid = Member("Grim", discordUserId);
        var state = new LoreEditState("Grim", operation == "try-update", "original", null, []);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            operation switch
            {
                "create" => fixture.Store.CreateLoreAsync(invalid, TestContext.Current.CancellationToken),
                "update" => fixture.Store.UpdateLoreAsync(invalid, TestContext.Current.CancellationToken),
                "upsert" => fixture.Store.UpsertLoreAsync(invalid),
                "upsert-ilore" => fixture.Store.UpsertLoreAsync((ILore)invalid),
                "lookup" => fixture.Store.ReadMemberLoreAsync(discordUserId, TestContext.Current.CancellationToken),
                _ => fixture.Store.TrySaveAsync(state, Revision(state, LoreEntrySnapshot.FromLore(invalid)), TestContext.Current.CancellationToken),
            }
        );

        Assert.Null(fixture.ReadFilter);
        Assert.Equal(0, fixture.EmbeddingCalls);
        Assert.Null(fixture.Inserted);
        Assert.Null(fixture.Replacement);
    }

    public static IEnumerable<object[]> SupportedWriteBindings =>
        from operation in new[] { "create", "update", "upsert", "try-save" }
        from discordUserId in new[] { 1UL, DiscordUserId, (ulong)long.MaxValue }
        select new object[] { operation, discordUserId };

    [Theory]
    [MemberData(nameof(SupportedWriteBindings))]
    public async Task EveryWritePathPreservesBindingProfileAndBeforeAfterAuditSnapshots(string operation, ulong discordUserId)
    {
        var original = Member("Grim", discordUserId);
        var updated = original with { Biography = "Updated biography" };
        var fixture = new MemberLoreStoreFixture { ReadResults = operation == "create" ? [] : [new LoreDocument(original)] };
        if (operation == "create")
            Assert.True(await fixture.Store.CreateLoreAsync(updated, TestContext.Current.CancellationToken));
        else if (operation == "update")
            Assert.True(await fixture.Store.UpdateLoreAsync(updated, TestContext.Current.CancellationToken));
        else if (operation == "upsert")
            await fixture.Store.UpsertLoreAsync(updated);
        else
        {
            var state = await fixture.Store.GetEntryAsync("Grim", TestContext.Current.CancellationToken);
            Assert.True(
                await fixture.Store.TrySaveAsync(
                    state,
                    Revision(state, state.Entry! with { Biography = updated.Biography }),
                    TestContext.Current.CancellationToken
                )
            );
        }

        var saved = fixture.Inserted ?? fixture.Replacement!;
        Assert.Equal(updated, saved.ToModel());
        Assert.Equal((long)discordUserId, saved.ToBsonDocument()[Field(nameof(LoreDocument.DiscordUserId))].AsInt64);
        Assert.NotNull(saved.Embedding);
        var revision = Assert.Single(saved.History);
        Assert.Equal(discordUserId, revision.After!.DiscordUserId);
        Assert.Equal(updated.Aliases, revision.After.Aliases);
        Assert.Equal(updated.Roles, revision.After.Roles);
        if (operation == "create")
            Assert.Null(revision.Before);
        else
            Assert.Equal(LoreEntrySnapshot.FromLore(original), revision.Before);
    }

    public static IEnumerable<object[]> UnsupportedHistoryBindings =>
        from location in new[] { "before", "history-before", "history-after" }
        from discordUserId in new[] { 0UL, (ulong)long.MaxValue + 1, ulong.MaxValue }
        select new object[] { location, discordUserId };

    [Theory]
    [MemberData(nameof(UnsupportedHistoryBindings))]
    public async Task UnsupportedRevisionBindingsAreRejectedBeforeEmbeddingOrWriting(string location, ulong discordUserId)
    {
        var fixture = new MemberLoreStoreFixture();
        var snapshot = LoreEntrySnapshot.FromLore(Member("Grim", DiscordUserId));
        var invalid = snapshot with { DiscordUserId = discordUserId };
        var old = new LoreRevision(
            "old",
            DateTime.UtcNow,
            "lore-store",
            null,
            "update",
            location == "history-before" ? invalid : snapshot,
            location == "history-after" ? invalid : snapshot
        );
        var state = new LoreEditState("Grim", true, old.Id, snapshot, location == "before" ? [] : [old]);
        var revision = Revision(state, snapshot) with { Before = location == "before" ? invalid : snapshot };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            fixture.Store.TrySaveAsync(state, revision, TestContext.Current.CancellationToken)
        );

        Assert.Equal(0, fixture.EmbeddingCalls);
        Assert.Null(fixture.Inserted);
        Assert.Null(fixture.Replacement);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RebindingAndUnbindingPreserveProfileFieldsAndUndoRestoresThePriorBinding(bool unbind)
    {
        var member = Member("Grim", DiscordUserId);
        var fixture = new MemberLoreStoreFixture { ReadResults = [new LoreDocument(member)] };
        var state = await fixture.Store.GetEntryAsync(member.Name, TestContext.Current.CancellationToken);
        var next = state.Entry! with { DiscordUserId = unbind ? null : DiscordUserId + 1 };
        var revision = Revision(state, next);

        Assert.True(await fixture.Store.TrySaveAsync(state, revision, TestContext.Current.CancellationToken));
        Assert.Equal(member with { DiscordUserId = next.DiscordUserId }, fixture.Replacement!.ToModel());
        Assert.Equal(DiscordUserId, fixture.Replacement.History[^1].Before!.DiscordUserId);
        Assert.Equal(next.DiscordUserId, fixture.Replacement.History[^1].After!.DiscordUserId);

        fixture.ReadResults = [fixture.Replacement];
        var edited = await fixture.Store.GetEntryAsync(member.Name, TestContext.Current.CancellationToken);
        var undo = Revision(edited, edited.History[^1].Before!) with { Action = "undo" };
        Assert.True(await fixture.Store.TrySaveAsync(edited, undo, TestContext.Current.CancellationToken));
        Assert.Equal(member, fixture.Replacement!.ToModel());
        Assert.Equal(undo, fixture.Replacement.History[^1]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DuplicateAccountKeysRejectBothInsertsAndReplacements(bool replace)
    {
        var fixture = new MemberLoreStoreFixture
        {
            ReadResults = replace ? [new LoreDocument(Member("Grim", null))] : [],
            InsertFailure = DuplicateKey(),
            ReplaceFailure = DuplicateKey(),
        };

        var saved = replace
            ? await fixture.Store.UpdateLoreAsync(Member("Grim", DiscordUserId), TestContext.Current.CancellationToken)
            : await fixture.Store.CreateLoreAsync(Member("Grim", DiscordUserId), TestContext.Current.CancellationToken);

        Assert.False(saved);
        Assert.Equal(1, fixture.EmbeddingCalls);
        Assert.NotNull(replace ? fixture.Replacement : fixture.Inserted);
    }

    [Fact]
    public async Task NonDuplicateReplacementFailuresPropagate()
    {
        var failure = new InvalidOperationException("Database unavailable");
        var fixture = new MemberLoreStoreFixture { ReadResults = [new LoreDocument(Member("Grim", null))], ReplaceFailure = failure };

        Assert.Same(
            failure,
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                fixture.Store.UpdateLoreAsync(Member("Grim", DiscordUserId), TestContext.Current.CancellationToken)
            )
        );
    }

    [Fact]
    public void UniqueIndexCoversAllPositiveBindingsAndTombstonesReleaseTheirAccountMetadata()
    {
        var index = Collections.MemberDiscordUserIdIndex;
        Assert.Equal(1, index.Keys.Render(RenderArgs())[Field(nameof(LoreDocument.DiscordUserId))].AsInt32);
        var options = Assert.IsType<CreateIndexOptions<LoreDocument>>(index.Options);
        Assert.True(options.Unique);
        Assert.Equal("unique_active_member_discord_user_id", options.Name);
        var filter = Render(options.PartialFilterExpression);
        var binding = filter[Field(nameof(LoreDocument.DiscordUserId))];
        Assert.Equal(0L, binding["$gt"].AsInt64);
        Assert.False(filter.Contains(Field(nameof(LoreDocument.Deleted))));
        Assert.False(new LoreDocument(Member("Grim", DiscordUserId)).ToBsonDocument()[Field(nameof(LoreDocument.Deleted))].AsBoolean);
        Assert.False(new LoreDocument(Member("Legacy", null)).ToBsonDocument().Contains(Field(nameof(LoreDocument.DiscordUserId))));
        Assert.False((new LoreDocument("Deleted", "") { Deleted = true }).ToBsonDocument().Contains(Field(nameof(LoreDocument.DiscordUserId))));
    }

    private static MemberLore Member(string name, ulong? discordUserId) =>
        new(name, "they", "UK", "Shaman", "Elevator joke")
        {
            DiscordUserId = discordUserId,
            Aliases = ["Grimbles"],
            Roles = ["Raider"],
        };

    private static LoreRevision Revision(LoreEditState state, LoreEntrySnapshot after) =>
        new(Guid.NewGuid().ToString("N"), DateTime.UtcNow, "admin-dm", 1, "update", state.Entry, after);

    private static string Field(string property) => BsonClassMap.LookupClassMap(typeof(LoreDocument)).GetMemberMap(property).ElementName;

    private static RenderArgs<LoreDocument> RenderArgs() =>
        new(BsonSerializer.SerializerRegistry.GetSerializer<LoreDocument>(), BsonSerializer.SerializerRegistry);

    private static BsonDocument Render(FilterDefinition<LoreDocument> filter) => filter.Render(RenderArgs());

    private static MongoWriteException DuplicateKey()
    {
        var error = (WriteError)
            Activator.CreateInstance(
                typeof(WriteError),
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                [ServerErrorCategory.DuplicateKey, 11000, "Duplicate Discord binding", new BsonDocument()],
                null
            )!;
        return new MongoWriteException(new ConnectionId(new ServerId(new ClusterId(), new DnsEndPoint("localhost", 27017))), error, null, null);
    }
}
