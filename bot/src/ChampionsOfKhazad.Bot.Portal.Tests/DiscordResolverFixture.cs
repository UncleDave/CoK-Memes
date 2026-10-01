using System.Collections.Concurrent;
using Discord;

namespace ChampionsOfKhazad.Bot.Portal.Tests;

internal sealed class DiscordResolverFixture : IDisposable
{
    private readonly ConcurrentDictionary<ulong, IGuildUser> _members = [];
    private int _guildLookups;
    private int _membershipLookups;
    private int _profileLookups;
    public bool IsMember { get; set; } = true;
    public int GuildLookups => _guildLookups;
    public int MembershipLookups => _membershipLookups;
    public int ProfileLookups => _profileLookups;
    public Func<Task>? BeforeGuildRead { get; set; }
    public Func<Task>? BeforeMembershipRead { get; set; }
    public IGuildUser Member => GetMember(42);
    public IUser Profile { get; } =
        DiscordTestProxy.Create<IUser>((method, _) => method.Name == "get_Id" ? 42UL : throw new NotSupportedException(method.Name));
    public TestClock Clock { get; } = new();
    public DiscordUserResolver Resolver { get; }

    public DiscordResolverFixture()
    {
        var guild = DiscordTestProxy.Create<IGuild>(
            (method, args) => method.Name == "GetUserAsync" ? ReadMemberAsync((ulong)args![0]!) : throw new NotSupportedException(method.Name)
        );
        var client = DiscordTestProxy.Create<IDiscordClient>(
            (method, _) =>
            {
                if (method.Name == "GetGuildAsync")
                    return ReadGuildAsync(guild);
                if (method.Name != "GetUserAsync")
                    throw new NotSupportedException(method.Name);
                Interlocked.Increment(ref _profileLookups);
                return Task.FromResult(Profile);
            }
        );
        Resolver = new DiscordUserResolver(new ClientProvider(client), new(1), Clock);
    }

    private IGuildUser GetMember(ulong id) =>
        _members.GetOrAdd(
            id,
            userId =>
                DiscordTestProxy.Create<IGuildUser>(
                    (method, _) =>
                        method.Name switch
                        {
                            "get_Id" => userId,
                            "get_GuildId" => 1UL,
                            _ => throw new NotSupportedException(method.Name),
                        }
                )
        );

    private async Task<IGuild> ReadGuildAsync(IGuild guild)
    {
        Interlocked.Increment(ref _guildLookups);
        if (BeforeGuildRead is not null)
            await BeforeGuildRead();
        return guild;
    }

    private async Task<IGuildUser> ReadMemberAsync(ulong id)
    {
        Interlocked.Increment(ref _membershipLookups);
        if (BeforeMembershipRead is not null)
            await BeforeMembershipRead();
        return IsMember ? GetMember(id) : null!;
    }

    public void Dispose() => Resolver.Dispose();

    internal sealed class TestClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan time) => _now += time;
    }

    private sealed class ClientProvider(IDiscordClient client) : IDiscordClientProvider
    {
        public Task<IDiscordClient> GetClientAsync() => Task.FromResult(client);
    }
}
