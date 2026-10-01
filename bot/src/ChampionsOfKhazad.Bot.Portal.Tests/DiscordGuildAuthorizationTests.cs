using System.Security.Claims;
using Discord;
using Microsoft.AspNetCore.Authorization;

namespace ChampionsOfKhazad.Bot.Portal.Tests;

public class DiscordGuildAuthorizationTests
{
    [Fact]
    public async Task LeavingGuildRevokesAccessAfterAnEarlierSuccessfulLookup()
    {
        var fixture = new ResolverFixture();
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
        var fixture = new ResolverFixture { IsMember = false };

        Assert.Same(fixture.Profile, await fixture.Resolver.GetUserAsync(42));
        Assert.Null(await fixture.Resolver.GetGuildUserAsync(42));
        Assert.Equal(1, fixture.ProfileLookups);
    }

    [Fact]
    public async Task ConcurrentLookupsDoNotShareMutableUserCache()
    {
        var fixture = new ResolverFixture();
        var users = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => fixture.Resolver.GetUserAsync(42)));

        Assert.All(users, user => Assert.Same(fixture.Member, user));
        Assert.Equal(20, fixture.MembershipLookups);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("invalid")]
    [InlineData("oauth2|discord|not-a-number")]
    public async Task InvalidIdentityFailsWithoutCallingDiscord(string? identity)
    {
        var fixture = new ResolverFixture();
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

    private sealed class ResolverFixture
    {
        private int _membershipLookups;
        private int _profileLookups;
        public bool IsMember { get; set; } = true;
        public int MembershipLookups => _membershipLookups;
        public int ProfileLookups => _profileLookups;
        public IGuildUser Member { get; } =
            DiscordTestProxy.Create<IGuildUser>(
                (method, _) =>
                    method.Name switch
                    {
                        "get_Id" => 42UL,
                        "get_GuildId" => 1UL,
                        _ => throw new NotSupportedException(method.Name),
                    }
            );
        public IUser Profile { get; } =
            DiscordTestProxy.Create<IUser>((method, _) => method.Name == "get_Id" ? 42UL : throw new NotSupportedException(method.Name));
        public DiscordUserResolver Resolver { get; }

        public ResolverFixture()
        {
            var guild = DiscordTestProxy.Create<IGuild>(
                (method, _) =>
                {
                    if (method.Name != "GetUserAsync")
                        throw new NotSupportedException(method.Name);
                    Interlocked.Increment(ref _membershipLookups);
                    return Task.FromResult(IsMember ? Member : null!);
                }
            );
            var client = DiscordTestProxy.Create<IDiscordClient>(
                (method, _) =>
                {
                    if (method.Name == "GetGuildAsync")
                        return Task.FromResult(guild);
                    if (method.Name != "GetUserAsync")
                        throw new NotSupportedException(method.Name);
                    Interlocked.Increment(ref _profileLookups);
                    return Task.FromResult(Profile);
                }
            );
            Resolver = new DiscordUserResolver(new ClientProvider(client), new(1));
        }
    }

    private sealed class ClientProvider(IDiscordClient client) : IDiscordClientProvider
    {
        public Task<IDiscordClient> GetClientAsync() => Task.FromResult(client);
    }
}
