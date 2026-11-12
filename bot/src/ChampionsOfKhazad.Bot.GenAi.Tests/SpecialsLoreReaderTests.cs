using System.Text.Json;
using ChampionsOfKhazad.Bot.Lore.Abstractions;

namespace ChampionsOfKhazad.Bot.GenAi.Tests;

public class SpecialsLoreReaderTests
{
    private static readonly NotebookSource Source = new(
        "https://discord.com/channels/1/3/42",
        9,
        "Server Nick",
        DateTime.UtcNow,
        "Recent chat evidence"
    );
    private static readonly SpecialsContext Context = new(new("A supported recent event", [Source.Url]), [Source]);

    [Fact]
    public async Task RetrievesGuildCallbacksAndExactLinkedBiographiesWithoutCopyingPersonalProfileFieldsOrGuessingAliases()
    {
        var store = new Store();
        store.Members[9] = new Member("Canonical Member", "An established lift joke.", 9);
        var search = new Search
        {
            Results = [new Guild("Old guild story", "The guild's established joke."), new Member("Server Nick", "UNLINKED BIOGRAPHY", null)],
        };
        var batch = await new SpecialsLoreReader(store, search).ReadAsync([Context, Context], TestContext.Current.CancellationToken);
        Assert.Equal(new[] { "Old guild story", "Canonical Member" }, batch.Contexts[0].Lore.Select(entry => entry.Name));
        Assert.Equal(new ulong[] { 9 }, store.MemberRequests);
        Assert.Equal(2, search.Queries.Count);
        Assert.All(
            search.Queries,
            query =>
            {
                Assert.Contains(Context.Event.Summary, query);
                Assert.Contains(Source.AuthorName, query);
                Assert.DoesNotContain(Source.Content, query);
            }
        );
        Assert.Equal(batch.Contexts[0].Lore, batch.Contexts[1].Lore);
        Assert.Equal(9UL, batch.Contexts[0].Lore[1].DiscordUserId);
        var data = JsonSerializer.Serialize(batch);
        Assert.DoesNotContain("SECRET personal field", data);
        Assert.DoesNotContain("UNLINKED BIOGRAPHY", data);
        Assert.Equal(0, batch.OmittedEntries);
    }

    [Fact]
    public async Task SearchResultsAndMismatchedAccountLinksCannotImpersonateCurrentParticipants()
    {
        var store = new Store();
        store.Members[9] = new Member("Wrong account", "Wrong biography", 99);
        var search = new Search { Results = [new Member("Server Nick", "Matching name, wrong identity", 9)] };
        var batch = await new SpecialsLoreReader(store, search).ReadAsync([Context], TestContext.Current.CancellationToken);
        Assert.Empty(Assert.Single(batch.Contexts).Lore);
    }

    [Fact]
    public async Task LookupScopeAndAttachedEntriesStayBoundedAndPrioritiseEvidenceAnchorIdentities()
    {
        var sources = Enumerable
            .Range(1, 20)
            .Select(index => Source with { Url = $"https://discord.com/channels/1/3/{index}", AuthorId = (ulong)index })
            .ToArray();
        var anchor = sources[15] with { MentionedUsers = [new(77, "Mentioned member")] };
        sources[15] = anchor;
        var context = new SpecialsContext(new("Event", [anchor.Url]), sources);
        var store = new Store();
        var search = new Search { Results = Enumerable.Range(1, 10).Select(index => (ILore)new Guild($"Entry {index}", "Complete lore")).ToArray() };
        var batch = await new SpecialsLoreReader(store, search).ReadAsync([context], TestContext.Current.CancellationToken);
        Assert.Equal(new ulong[] { 16, 77, 1 }, store.MemberRequests);
        Assert.Equal(3, Assert.Single(batch.Contexts).Lore.Count);
        Assert.Single(search.Queries);
    }

    [Fact]
    public async Task OversizedEntriesAreSkippedWholeAndSerializedBudgetCountsRepeatedAttachments()
    {
        var store = new Store();
        var search = new Search
        {
            Results =
            [
                new Guild("Oversized", new string('x', SpecialsLoreReader.MaximumEntryCharacters + 1)),
                new Guild("Unicode", new string('界', 3000)),
                new Guild("Fits", new string('x', 3900)),
                new Guild("Also fits", new string('x', 3900)),
            ],
        };
        var batch = await new SpecialsLoreReader(store, search).ReadAsync([Context, Context, Context], TestContext.Current.CancellationToken);
        Assert.True(batch.OmittedEntries > 0);
        var attached = batch.Contexts.SelectMany(context => context.Lore).ToArray();
        Assert.DoesNotContain(attached, entry => entry.Name is "Oversized" or "Unicode");
        Assert.All(attached, entry => Assert.Equal(3900, entry.Content.Length));
        Assert.True(JsonSerializer.Serialize(attached).Length <= SpecialsLoreReader.MaximumSerializedCharacters);
        Assert.InRange(attached.Length, 1, 3);
    }

    [Theory]
    [InlineData("content")]
    [InlineData("account")]
    [InlineData("type")]
    [InlineData("deleted")]
    [InlineData("name")]
    public async Task UsedLoreVerificationRejectsChangedDeletedOrReboundEntries(string change)
    {
        var store = new Store();
        var original = new Member("Canonical Member", "Lift joke", 9);
        store.Entries[original.Name] = original;
        var reader = new SpecialsLoreReader(store, new Search());
        var reference = new SpecialsLoreReference("L1", original.Name, original.Biography!, 9);
        Assert.True(await reader.VerifyAsync([reference], TestContext.Current.CancellationToken));
        store.Entries[original.Name] = change switch
        {
            "content" => original with { Biography = "Edited joke" },
            "account" => original with { DiscordUserId = 10 },
            "type" => new Guild(original.Name, original.Biography!),
            "name" => original with { Name = "Renamed Member" },
            _ => null,
        };
        Assert.False(await reader.VerifyAsync([reference], TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task EmptyInputsAvoidLookupsAndCallerCancellationPropagates()
    {
        var store = new Store();
        var search = new Search();
        var reader = new SpecialsLoreReader(store, search);
        Assert.Empty((await reader.ReadAsync([], TestContext.Current.CancellationToken)).Contexts);
        Assert.True(await reader.VerifyAsync([], TestContext.Current.CancellationToken));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.ReadAsync([Context], cancellation.Token));
        Assert.Empty(store.MemberRequests);
        Assert.Empty(search.Queries);
    }

    private sealed record Guild(string Name, string Content) : IGuildLore;

    private sealed record Member(string Name, string? Biography, ulong? DiscordUserId) : IMemberLore
    {
        public string Pronouns => "SECRET personal field";
        public string Nationality => "SECRET personal field";
        public string MainCharacter => "SECRET personal field";
        public IReadOnlyList<string> Aliases => ["SECRET personal field"];
        public IReadOnlyList<string> Roles => ["SECRET personal field"];
    }

    private sealed class Search : IGetRelatedLore
    {
        public IReadOnlyList<ILore> Results { get; set; } = [];
        public List<string> Queries { get; } = [];

        public Task<IReadOnlyList<ILore>> GetRelatedLoreAsync(string text, uint max = 10)
        {
            Assert.Equal(4U, max);
            Queries.Add(text);
            return Task.FromResult(Results);
        }
    }

    private sealed class Store : IGetLore
    {
        public Dictionary<ulong, IMemberLore> Members { get; } = [];
        public Dictionary<string, ILore?> Entries { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<ulong> MemberRequests { get; } = [];

        public Task<IReadOnlyList<ILore>> GetLoreAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<ILore?> GetLoreAsync(string name, CancellationToken cancellationToken = default) =>
            Task.FromResult(Entries.GetValueOrDefault(name));

        public Task<IMemberLore?> GetMemberLoreAsync(ulong discordUserId, CancellationToken cancellationToken = default)
        {
            MemberRequests.Add(discordUserId);
            return Task.FromResult(Members.GetValueOrDefault(discordUserId));
        }
    }
}
