using System.Net;
using System.Net.Http.Json;
using ChampionsOfKhazad.Bot.Lore;
using ChampionsOfKhazad.Bot.Lore.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace ChampionsOfKhazad.Bot.Portal.Tests;

public class LoreMutationEndpointTests
{
    [Theory]
    [InlineData("guild-lore")]
    [InlineData("member-lore")]
    public async Task SuccessfulCreateReturnsCreatedAndBindsTheExpectedLore(string route)
    {
        var store = new LoreStoreStub();
        await using var app = await CreateAppAsync(store);
        using var client = app.GetTestClient();
        using var response = await client.PostAsync($"/api/{route}", CreateBody(route, "Entry", "Original"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("/api/lore/Entry", response.Headers.Location!.OriginalString);
        Assert.NotNull(store.Created);
        Assert.Equal("Entry", store.Created.Name);
        Assert.Equal("Original", route == "guild-lore" ? ((IGuildLore)store.Created).Content : ((IMemberLore)store.Created).Biography);
        Assert.Null(store.Updated);
    }

    [Theory]
    [InlineData("guild-lore")]
    [InlineData("member-lore")]
    public async Task RejectedCreateReturnsConflictWithoutTryingAnUpdate(string route)
    {
        var store = new LoreStoreStub { CreateResult = false };
        await using var app = await CreateAppAsync(store);
        using var client = app.GetTestClient();
        using var response = await client.PostAsync(
            $"/api/{route}",
            CreateBody(route, "Entry", "Replacement"),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(TestContext.Current.CancellationToken);
        Assert.Equal("Lore with this name already exists.", error!["message"]);
        Assert.NotNull(store.Created);
        Assert.Null(store.Updated);
    }

    [Theory]
    [InlineData("guild-lore")]
    [InlineData("member-lore")]
    public async Task MissingUpdateReturnsNotFoundWithoutTryingACreate(string route)
    {
        var store = new LoreStoreStub { UpdateResult = false };
        await using var app = await CreateAppAsync(store);
        using var client = app.GetTestClient();
        using var response = await client.PutAsync(
            $"/api/{route}/Missing",
            CreateBody(route, "Missing", "Content"),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        if (route == "member-lore")
            Assert.Null(store.Updated);
        else
            Assert.NotNull(store.Updated);
        Assert.Null(store.Created);
    }

    [Theory]
    [InlineData("guild-lore")]
    [InlineData("member-lore")]
    public async Task SuccessfulUpdateReturnsNoContentAndBindsTheExpectedLore(string route)
    {
        var store = new LoreStoreStub();
        await using var app = await CreateAppAsync(store);
        using var client = app.GetTestClient();

        using var response = await client.PutAsync(
            $"/api/{route}/Entry",
            CreateBody(route, "Entry", "Updated"),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.NotNull(store.Updated);
        Assert.Equal("Entry", store.Updated.Name);
        Assert.Equal("Updated", route == "guild-lore" ? ((IGuildLore)store.Updated).Content : ((IMemberLore)store.Updated).Biography);
        Assert.Null(store.Created);
    }

    [Theory]
    [InlineData("guild-lore")]
    [InlineData("member-lore")]
    public async Task StoreFailuresPropagateInsteadOfReturningSuccessfulResults(string route)
    {
        var failure = new InvalidOperationException("Store unavailable");
        var store = new LoreStoreStub { WriteFailure = failure };
        await using var app = await CreateAppAsync(store);
        using var client = app.GetTestClient();

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.PostAsync($"/api/{route}", CreateBody(route, "Entry", "Content"), TestContext.Current.CancellationToken)
        );
        Assert.Same(failure, actual);
    }

    [Theory]
    [InlineData("1", 1UL)]
    [InlineData("9007199254740993", 9007199254740993UL)]
    [InlineData("9223372036854775807", (ulong)long.MaxValue)]
    public async Task MemberDiscordUserIdsBindExactlyForCreatesAndUpdates(string discordUserId, ulong expected)
    {
        var store = new LoreStoreStub();
        await using var app = await CreateAppAsync(store);
        using var client = app.GetTestClient();

        using var created = await client.PostAsync("/api/member-lore", MemberBody(discordUserId), TestContext.Current.CancellationToken);
        using var updated = await client.PutAsync("/api/member-lore/Entry", MemberBody(discordUserId), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(expected, Assert.IsAssignableFrom<IMemberLore>(store.Created).DiscordUserId);
        Assert.Equal(HttpStatusCode.NoContent, updated.StatusCode);
        Assert.Equal(expected, Assert.IsAssignableFrom<IMemberLore>(store.Updated).DiscordUserId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task CreatingWithoutADiscordUserIdLeavesTheMemberUnlinked(string? discordUserId)
    {
        var store = new LoreStoreStub();
        await using var app = await CreateAppAsync(store);
        using var client = app.GetTestClient();
        using var response = await client.PostAsync("/api/member-lore", MemberBody(discordUserId), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Null(Assert.IsAssignableFrom<IMemberLore>(store.Created).DiscordUserId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OmittedOrNullDiscordUserIdPreservesTheExistingMemberLink(bool includeDiscordUserId)
    {
        var existing = new MemberLore("Entry", "they", "UK", "Character", "Original") { DiscordUserId = long.MaxValue };
        var store = new LoreStoreStub();
        await using var app = await CreateAppAsync(store, new LoreGetterStub(existing));
        using var client = app.GetTestClient();
        using var response = await client.PutAsync(
            "/api/member-lore/Entry",
            MemberBody(null, includeDiscordUserId),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal((ulong)long.MaxValue, Assert.IsAssignableFrom<IMemberLore>(store.Updated).DiscordUserId);
        Assert.Equal("Updated", Assert.IsAssignableFrom<IMemberLore>(store.Updated).Biography);
    }

    [Fact]
    public async Task ExplicitEmptyDiscordUserIdClearsTheExistingMemberLink()
    {
        var existing = new MemberLore("Entry", "they", "UK", "Character", "Original") { DiscordUserId = long.MaxValue };
        var store = new LoreStoreStub();
        await using var app = await CreateAppAsync(store, new LoreGetterStub(existing));
        using var client = app.GetTestClient();
        using var response = await client.PutAsync("/api/member-lore/Entry", MemberBody(""), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Null(Assert.IsAssignableFrom<IMemberLore>(store.Updated).DiscordUserId);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("00")]
    [InlineData("-1")]
    [InlineData("+1")]
    [InlineData("1.0")]
    [InlineData("1e2")]
    [InlineData(" 1")]
    [InlineData("1 ")]
    [InlineData("abc")]
    [InlineData("١")]
    [InlineData("9223372036854775808")]
    [InlineData("18446744073709551615")]
    [InlineData("18446744073709551616")]
    public async Task InvalidDiscordUserIdsReturnActionableBadRequestsWithoutWriting(string discordUserId)
    {
        var store = new LoreStoreStub();
        await using var app = await CreateAppAsync(store);
        using var client = app.GetTestClient();
        foreach (var method in new[] { HttpMethod.Post, HttpMethod.Put })
        {
            using var request = new HttpRequestMessage(method, method == HttpMethod.Post ? "/api/member-lore" : "/api/member-lore/Entry")
            {
                Content = MemberBody(discordUserId),
            };
            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var error = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(TestContext.Current.CancellationToken);
            Assert.Contains("Discord user ID", error!["message"]);
            Assert.Contains("9223372036854775807", error["message"]);
            Assert.Contains("blank", error["message"]);
        }
        Assert.Null(store.Created);
        Assert.Null(store.Updated);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DiscordUserIdsLinkedToAnotherMemberReturnConflictWithoutWriting(bool update)
    {
        var linked = new MemberLore("Other Member", "they", "UK", "Character", "Original") { DiscordUserId = long.MaxValue };
        var existing = new MemberLore("Entry", "they", "UK", "Character", "Original");
        var store = new LoreStoreStub();
        await using var app = await CreateAppAsync(store, new LoreGetterStub(linked, existing));
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(update ? HttpMethod.Put : HttpMethod.Post, update ? "/api/member-lore/Entry" : "/api/member-lore")
        {
            Content = MemberBody("9223372036854775807"),
        };
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(TestContext.Current.CancellationToken);
        Assert.Contains("9223372036854775807", error!["message"]);
        Assert.Contains("Other Member", error["message"]);
        Assert.Contains("Clear", error["message"]);
        Assert.Null(store.Created);
        Assert.Null(store.Updated);
    }

    [Fact]
    public async Task ExistingMemberCanKeepItsOwnDiscordUserIdWithCaseInsensitiveNameLookup()
    {
        var existing = new MemberLore("Entry", "they", "UK", "Character", "Original")
        {
            DiscordUserId = long.MaxValue,
            Aliases = ["Old alias"],
            Roles = ["Old role"],
        };
        var store = new LoreStoreStub();
        var getter = new LoreGetterStub(existing);
        var editor = new LoreEditorStub(store, getter);
        await using var app = await CreateAppAsync(store, getter, editor);
        using var client = app.GetTestClient();
        using var response = await client.PutAsync(
            "/api/member-lore/eNtRy",
            MemberBody("9223372036854775807"),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var updated = Assert.IsAssignableFrom<IMemberLore>(store.Updated);
        Assert.Equal((ulong)long.MaxValue, updated.DiscordUserId);
        Assert.Equal("Entry", updated.Name);
        Assert.Equal("Updated", updated.Biography);
        Assert.Empty(updated.Aliases);
        Assert.Empty(updated.Roles);
        Assert.NotNull(editor.Expected);
        Assert.NotNull(editor.AttemptedRevision);
        Assert.Same(editor.Expected.Entry, editor.AttemptedRevision.Before);
        Assert.Equal("version-one", editor.Expected.Revision);
        Assert.Equal("Entry", editor.AttemptedRevision.After!.Name);
        Assert.Equal("lore-store", editor.AttemptedRevision.Source);
        Assert.Null(editor.AttemptedRevision.ActorId);
        Assert.Equal("save", editor.AttemptedRevision.Action);
        Assert.True(Guid.TryParseExact(editor.AttemptedRevision.Id, "N", out _));
        Assert.Equal(DateTimeKind.Utc, editor.AttemptedRevision.CreatedAtUtc.Kind);
        Assert.Equal(editor.EntryToken, editor.SaveToken);
    }

    [Fact]
    public async Task RejectedExistingMemberUpdateReturnsAnActionableConflict()
    {
        var existing = new MemberLore("Entry", "they", "UK", "Character", "Original");
        var store = new LoreStoreStub { UpdateResult = false };
        await using var app = await CreateAppAsync(store, new LoreGetterStub(existing));
        using var client = app.GetTestClient();
        using var response = await client.PutAsync("/api/member-lore/Entry", MemberBody(""), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(TestContext.Current.CancellationToken);
        Assert.Contains("Reload", error!["message"]);
        Assert.NotNull(store.Updated);
        Assert.Null(store.Created);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DuplicateDiscordLinkDuringTheWriteReturnsConflictWithTheLinkedMember(bool update)
    {
        var linked = new MemberLore("Concurrent Member", "they", "UK", "Character", "Original") { DiscordUserId = long.MaxValue };
        var existing = new MemberLore("Entry", "they", "UK", "Character", "Original");
        var identityLookups = 0;
        var getter = new LoreGetterStub(existing) { FindMember = _ => ++identityLookups == 1 ? null : linked };
        var store = new LoreStoreStub { CreateResult = false, UpdateResult = false };
        await using var app = await CreateAppAsync(store, getter);
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(update ? HttpMethod.Put : HttpMethod.Post, update ? "/api/member-lore/Entry" : "/api/member-lore")
        {
            Content = MemberBody("9223372036854775807"),
        };
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(TestContext.Current.CancellationToken);
        Assert.Contains("Concurrent Member", error!["message"]);
        Assert.Contains("9223372036854775807", error["message"]);
        Assert.Equal(2, identityLookups);
        if (update)
        {
            Assert.NotNull(store.Updated);
            Assert.Null(store.Created);
        }
        else
        {
            Assert.NotNull(store.Created);
            Assert.Null(store.Updated);
        }
    }

    [Fact]
    public async Task MemberUpdateCannotReplaceGuildLore()
    {
        var store = new LoreStoreStub();
        await using var app = await CreateAppAsync(store, new LoreGetterStub(new GuildLore("Entry", "Original guild content")));
        using var client = app.GetTestClient();
        using var response = await client.PutAsync("/api/member-lore/Entry", MemberBody(""), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Null(store.Updated);
        Assert.Null(store.Created);
    }

    [Theory]
    [InlineData(false, null, null)]
    [InlineData(true, null, null)]
    [InlineData(true, "9007199254740993", null)]
    [InlineData(false, null, 42UL)]
    [InlineData(true, null, 42UL)]
    [InlineData(true, "9007199254740993", 42UL)]
    [InlineData(true, "", 42UL)]
    public async Task ConcurrentUnlinkOrRebindRejectsTheWholeMemberUpdate(
        bool includeDiscordUserId,
        string? submittedDiscordUserId,
        ulong? concurrentDiscordUserId
    )
    {
        var existing = new MemberLore("Entry", "they", "UK", "Character", "Original") { DiscordUserId = 9007199254740993UL };
        var store = new LoreStoreStub();
        var getter = new LoreGetterStub(existing);
        var editor = new LoreEditorStub(store, getter)
        {
            BeforeSave = current =>
            {
                var state = current.Current!;
                current.Current = state with
                {
                    Revision = "concurrent-revision",
                    Entry = state.Entry! with { DiscordUserId = concurrentDiscordUserId, Biography = "Concurrent biography" },
                };
            },
        };
        await using var app = await CreateAppAsync(store, getter, editor);
        using var client = app.GetTestClient();
        using var response = await client.PutAsync(
            "/api/member-lore/Entry",
            MemberBody(submittedDiscordUserId, includeDiscordUserId),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(TestContext.Current.CancellationToken);
        Assert.Contains("Reload", error!["message"]);
        Assert.NotNull(editor.Expected);
        Assert.Equal("version-one", editor.Expected.Revision);
        Assert.NotNull(editor.AttemptedRevision);
        Assert.Same(editor.Expected.Entry, editor.AttemptedRevision.Before);
        Assert.Equal("Updated", editor.AttemptedRevision.After!.Biography);
        Assert.Equal(submittedDiscordUserId == "" ? (ulong?)null : 9007199254740993UL, editor.AttemptedRevision.After.DiscordUserId);
        Assert.NotNull(editor.Current);
        Assert.Equal("concurrent-revision", editor.Current.Revision);
        Assert.Equal(concurrentDiscordUserId, editor.Current.Entry!.DiscordUserId);
        Assert.Equal("Concurrent biography", editor.Current.Entry.Biography);
        Assert.Empty(editor.Current.History);
        Assert.Null(store.Updated);
        Assert.Null(store.Created);
    }

    private static async Task<WebApplication> CreateAppAsync(IStoreLore store, IGetLore? loreGetter = null, IEditLoreStore? loreEditor = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddAuthentication();
        builder.Services.AddAuthorization();
        builder.Services.AddBot(_ => { }).AddGuildLore();
        builder.Services.RemoveAll<IGetRelatedLore>();
        builder.Services.AddSingleton(store);
        loreGetter ??= new LoreGetterStub(new MemberLore("Entry", "they", "UK", "Character", "Original"));
        builder.Services.AddSingleton(loreGetter);
        builder.Services.AddSingleton<IEditLoreStore>(loreEditor ?? new LoreEditorStub(store, loreGetter));
        var app = builder.Build();
        app.UseAuthorization();
        // Isolate HTTP binding/status behavior here; membership authorization has its own tests.
        var policy = new AuthorizationPolicyBuilder().RequireAssertion(_ => true).Build();
        app.MapGroup("api").MapLoreMutations(policy);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    private static JsonContent CreateBody(string route, string name, string content) =>
        route == "guild-lore"
            ? JsonContent.Create(new CreateGuildLoreContract(name, content))
            : JsonContent.Create(new CreateMemberLoreContract(name, "they", "UK", "Character", content, [], []));

    private static JsonContent MemberBody(string? discordUserId, bool includeDiscordUserId = true)
    {
        var body = new Dictionary<string, object?>
        {
            ["name"] = "Entry",
            ["pronouns"] = "they",
            ["nationality"] = "UK",
            ["mainCharacter"] = "Character",
            ["biography"] = "Updated",
            ["aliases"] = Array.Empty<string>(),
            ["roles"] = Array.Empty<string>(),
        };
        if (includeDiscordUserId)
            body["discordUserId"] = discordUserId;
        return JsonContent.Create(body);
    }

    private sealed class LoreGetterStub(params ILore[] entries) : IGetLore
    {
        public Func<ulong, IMemberLore?>? FindMember { get; init; }

        public Task<IReadOnlyList<ILore>> GetLoreAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ILore>>(entries);

        public Task<ILore?> GetLoreAsync(string name, CancellationToken cancellationToken = default) =>
            Task.FromResult(entries.FirstOrDefault(entry => string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase)));

        public Task<IMemberLore?> GetMemberLoreAsync(ulong discordUserId, CancellationToken cancellationToken = default) =>
            Task.FromResult(
                FindMember is null
                    ? entries.OfType<IMemberLore>().SingleOrDefault(member => member.DiscordUserId == discordUserId)
                    : FindMember(discordUserId)
            );
    }

    private sealed class LoreEditorStub(IStoreLore store, IGetLore loreGetter) : IEditLoreStore
    {
        public LoreEditState? Current { get; set; }
        public LoreEditState? Expected { get; private set; }
        public LoreRevision? AttemptedRevision { get; private set; }
        public CancellationToken EntryToken { get; private set; }
        public CancellationToken SaveToken { get; private set; }
        public Action<LoreEditorStub>? BeforeSave { get; init; }

        public Task<IReadOnlyList<LoreEditState>> GetEntriesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public async Task<LoreEditState> GetEntryAsync(string name, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EntryToken = cancellationToken;
            var lore = await loreGetter.GetLoreAsync(name, cancellationToken);
            Current ??= lore is null
                ? new LoreEditState(name, false, null, null, [])
                : new LoreEditState(lore.Name, true, "version-one", LoreEntrySnapshot.FromLore(lore), []);
            return Current;
        }

        public async Task<bool> TrySaveAsync(LoreEditState expected, LoreRevision revision, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Expected = expected;
            AttemptedRevision = revision;
            SaveToken = cancellationToken;
            BeforeSave?.Invoke(this);
            if (Current is null || Current.Name != expected.Name || Current.Exists != expected.Exists || Current.Revision != expected.Revision)
                return false;
            var after = revision.After!;
            var updated = await store.UpdateLoreAsync(
                new MemberLore(after.Name, after.Pronouns, after.Nationality, after.MainCharacter, after.Biography)
                {
                    Aliases = after.Aliases,
                    Roles = after.Roles,
                    DiscordUserId = after.DiscordUserId,
                },
                cancellationToken
            );
            if (updated)
                Current = Current with { Revision = revision.Id, Entry = after, History = Current.History.Append(revision).ToArray() };
            return updated;
        }
    }
}
