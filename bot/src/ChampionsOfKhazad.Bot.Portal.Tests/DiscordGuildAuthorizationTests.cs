using System.Security.Claims;
using Discord;
using Microsoft.AspNetCore.Authorization;

namespace ChampionsOfKhazad.Bot.Portal.Tests;

public class DiscordGuildAuthorizationTests
{
    [Fact]
    public async Task LeavingGuildRevokesAccessAfterAnEarlierSuccessfulLookup()
    {
        using var fixture = new DiscordResolverFixture();
        var handler = new DiscordGuildAuthorizationHandler(fixture.Resolver);

        Assert.Same(fixture.Member, await fixture.Resolver.GetUserAsync(42));
        var first = CreateContext();
        await handler.HandleAsync(first);
        Assert.True(first.HasSucceeded);

        fixture.IsMember = false;
        var second = CreateContext();
        await handler.HandleAsync(second);

        Assert.True(second.HasFailed);
        Assert.False(second.HasSucceeded);
        Assert.Equal(3, fixture.MembershipLookups);
        Assert.Equal(0, fixture.ProfileLookups);
    }

    [Fact]
    public async Task DisplayProfilesStillResolveForFormerMembers()
    {
        using var fixture = new DiscordResolverFixture { IsMember = false };

        Assert.Same(fixture.Profile, await fixture.Resolver.GetUserAsync(42));
        Assert.Null(await fixture.Resolver.GetGuildUserAsync(42));
        Assert.Equal(1, fixture.ProfileLookups);
    }

    [Fact]
    public async Task ConcurrentLookupsShareOneProfileLookup()
    {
        using var fixture = new DiscordResolverFixture();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.BeforeMembershipRead = () => release.Task.WaitAsync(TestContext.Current.CancellationToken);
        var requests = Enumerable.Range(0, 20).Select(_ => fixture.Resolver.GetUserAsync(42)).ToArray();
        IUser[] users;
        try
        {
            Assert.Equal(1, fixture.MembershipLookups);
            Assert.All(requests, request => Assert.False(request.IsCompleted));
        }
        finally
        {
            release.SetResult();
            users = await Task.WhenAll(requests);
        }

        Assert.All(users, user => Assert.Same(fixture.Member, user));
        Assert.Equal(1, fixture.GuildLookups);
        Assert.Equal(1, fixture.MembershipLookups);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("invalid")]
    [InlineData("oauth2|discord|not-a-number")]
    public async Task InvalidIdentityFailsWithoutCallingDiscord(string? identity)
    {
        using var fixture = new DiscordResolverFixture();
        var context = CreateContext(identity);

        await new DiscordGuildAuthorizationHandler(fixture.Resolver).HandleAsync(context);

        Assert.True(context.HasFailed);
        Assert.Equal(0, fixture.MembershipLookups);
    }

    private static AuthorizationHandlerContext CreateContext(string? identity = "oauth2|discord|42") =>
        new(
            [new DiscordGuildRequirement(1)],
            new ClaimsPrincipal(new ClaimsIdentity(identity is null ? [] : [new Claim(ClaimTypes.NameIdentifier, identity)], "test")),
            null
        );
}
