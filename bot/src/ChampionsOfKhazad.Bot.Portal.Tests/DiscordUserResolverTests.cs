namespace ChampionsOfKhazad.Bot.Portal.Tests;

public class DiscordUserResolverTests
{
    [Fact]
    public async Task RepeatedGalleryPagesReuseProfilesAndGuildMetadata()
    {
        using var fixture = new DiscordResolverFixture();
        var ids = Enumerable.Range(1, 20).Select(id => (ulong)id).ToArray();

        var first = await Task.WhenAll(ids.Select(fixture.Resolver.GetUserAsync));
        var second = await Task.WhenAll(ids.Select(fixture.Resolver.GetUserAsync));

        Assert.Equal(ids, first.Select(user => user.Id));
        Assert.Equal(first, second);
        Assert.Equal(1, fixture.GuildLookups);
        Assert.Equal(20, fixture.MembershipLookups);
    }

    [Fact]
    public async Task ConcurrentAuthorsShareOneGuildMetadataLookup()
    {
        using var fixture = new DiscordResolverFixture();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.BeforeGuildRead = () => release.Task.WaitAsync(TestContext.Current.CancellationToken);
        var requests = Enumerable.Range(1, 20).Select(id => fixture.Resolver.GetUserAsync((ulong)id)).ToArray();
        try
        {
            Assert.Equal(1, fixture.GuildLookups);
            Assert.Equal(0, fixture.MembershipLookups);
        }
        finally
        {
            release.SetResult();
            await Task.WhenAll(requests);
        }
        Assert.Equal(1, fixture.GuildLookups);
        Assert.Equal(20, fixture.MembershipLookups);
    }

    [Fact]
    public async Task DisplayCacheExpiresWithoutCachingMembershipAuthorization()
    {
        using var fixture = new DiscordResolverFixture();
        Assert.Same(fixture.Member, await fixture.Resolver.GetUserAsync(42));
        fixture.IsMember = false;

        Assert.Null(await fixture.Resolver.GetGuildUserAsync(42));
        Assert.Same(fixture.Member, await fixture.Resolver.GetUserAsync(42));
        fixture.Clock.Advance(TimeSpan.FromMinutes(15));
        Assert.Same(fixture.Profile, await fixture.Resolver.GetUserAsync(42));
        Assert.Equal(2, fixture.GuildLookups);
        Assert.Equal(3, fixture.MembershipLookups);
    }

    [Fact]
    public async Task FailedProfileLookupIsNotCached()
    {
        using var fixture = new DiscordResolverFixture { BeforeMembershipRead = () => throw new InvalidOperationException("Discord unavailable") };
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Resolver.GetUserAsync(42));
        fixture.BeforeMembershipRead = null;

        Assert.Same(fixture.Member, await fixture.Resolver.GetUserAsync(42));
        Assert.Equal(2, fixture.MembershipLookups);
    }

    [Fact]
    public async Task FailedGuildMetadataLookupCanBeRetried()
    {
        using var fixture = new DiscordResolverFixture { BeforeGuildRead = () => throw new InvalidOperationException("Discord unavailable") };
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Resolver.GetUserAsync(42));
        fixture.BeforeGuildRead = null;

        Assert.Same(fixture.Member, await fixture.Resolver.GetUserAsync(42));
        Assert.Equal(2, fixture.GuildLookups);
        Assert.Equal(1, fixture.MembershipLookups);
    }

    [Fact]
    public async Task ProfileCacheDoesNotRetainMoreThanItsCapacity()
    {
        using var fixture = new DiscordResolverFixture();
        for (ulong id = 1; id <= 501; id++)
        {
            await fixture.Resolver.GetUserAsync(id);
            fixture.Clock.Advance(TimeSpan.FromMilliseconds(1));
        }
        Assert.Equal(501, fixture.MembershipLookups);

        // At least one of 501 distinct authors must be fetched again from a cache capped at 500.
        // Do not pin the test to the framework's choice of eviction victim.
        for (ulong id = 1; id <= 501; id++)
            await fixture.Resolver.GetUserAsync(id);
        Assert.True(fixture.MembershipLookups > 501);
        Assert.Equal(1, fixture.GuildLookups);
    }
}
