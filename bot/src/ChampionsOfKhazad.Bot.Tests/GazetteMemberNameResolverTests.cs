using ChampionsOfKhazad.Bot.GenAi;

namespace ChampionsOfKhazad.Bot.Tests;

public class GazetteMemberNameResolverTests
{
    [Fact]
    public async Task MissingSocketMemberUsesRestServerNameOncePerMemberNotGlobalNames()
    {
        var requests = new List<ulong>();
        var resolver = new GazetteMemberNameResolver(
            (id, _) =>
            {
                requests.Add(id);
                return Task.FromResult<string?>(id == 1 ? "Crabslog" : "Guild Nick");
            },
            _ => null
        );
        var original = Source("Uncle Dave");
        var result = await resolver.ResolveAsync([original, original with { Url = "second" }], false, TestContext.Current.CancellationToken);
        Assert.Equal(new ulong[] { 1, 2 }, requests);
        Assert.All(result, source => Assert.Equal("Crabslog", source.AuthorName));
        Assert.Equal("Guild Nick", Assert.Single(result[0].MentionedUsers).Name);
        Assert.NotEqual(original.ViewHash, result[0].ViewHash);
        Assert.Equal(original.ContentHash, result[0].ContentHash);
    }

    [Fact]
    public async Task CachedGuildNicknameIsUsedForDiscoveryButVerificationRefreshesIt()
    {
        var calls = 0;
        var resolver = new GazetteMemberNameResolver(
            (_, _) =>
            {
                calls++;
                return Task.FromResult<string?>("New Nick");
            },
            _ => "Cached Nick"
        );
        var discovery = Assert.Single(await resolver.ResolveAsync([Source("Global name")], false, TestContext.Current.CancellationToken));
        Assert.Equal("Cached Nick", discovery.AuthorName);
        Assert.Equal(0, calls);
        var verification = Assert.Single(await resolver.ResolveAsync([Source("Global name")], true, TestContext.Current.CancellationToken));
        Assert.Equal("New Nick", verification.AuthorName);
        Assert.NotEqual(discovery.ViewHash, verification.ViewHash);
    }

    [Fact]
    public async Task UnknownGuildMemberIsNotSilentlyRenamedToTheirGlobalUsername()
    {
        var resolver = new GazetteMemberNameResolver((_, _) => Task.FromResult<string?>(null), _ => null);
        var source = Assert.Single(await resolver.ResolveAsync([Source("Uncle Dave")], false, TestContext.Current.CancellationToken));
        Assert.Equal("A guild member", source.AuthorName);
        Assert.Null(Assert.Single(source.MentionedUsers).Name);
    }

    private static NotebookSource Source(string author) =>
        new("source", 1, author, DateTime.UtcNow, "Discussion <@2>")
        {
            MentionedUsers = [new(2, "Global mention name")],
            ContentHash = "raw",
            ViewHash = "old",
        };
}
