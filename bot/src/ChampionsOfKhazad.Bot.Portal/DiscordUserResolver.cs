using Discord;

namespace ChampionsOfKhazad.Bot.Portal;

public record DiscordUserResolverOptions(ulong GuildId);

public class DiscordUserResolver(IDiscordClientProvider discordClientProvider, DiscordUserResolverOptions options, TimeProvider clock) : IDisposable
{
    private const int MaximumCachedProfiles = 500;
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(15);
    private readonly Lock _profilesLock = new();
    private readonly Dictionary<ulong, CachedProfile> _profiles = [];
    private readonly SemaphoreSlim _guildLock = new(1, 1);
    private IGuild? _guild;
    private DateTimeOffset _guildExpiresAt;

    public async Task<IGuildUser?> GetGuildUserAsync(ulong userId)
    {
        // Only guild metadata is cached here. Authorization always reads membership afresh.
        var guild = await GetGuildAsync();
        return await guild.GetUserAsync(userId);
    }

    public async Task<IUser> GetUserAsync(ulong userId)
    {
        CachedProfile profile;
        lock (_profilesLock)
        {
            var now = clock.GetUtcNow();
            if (!_profiles.TryGetValue(userId, out profile!) || profile.ExpiresAt <= now)
            {
                if (_profiles.Count >= MaximumCachedProfiles && !_profiles.ContainsKey(userId))
                    _profiles.Remove(_profiles.MinBy(entry => entry.Value.ExpiresAt).Key);
                // Sharing the task also coalesces concurrent display lookups for the same author.
                profile = new CachedProfile(LoadProfileAsync(userId), now + CacheLifetime);
                _profiles[userId] = profile;
            }
        }

        try
        {
            return await profile.User;
        }
        catch
        {
            lock (_profilesLock)
            {
                if (_profiles.TryGetValue(userId, out var current) && ReferenceEquals(current, profile))
                    _profiles.Remove(userId);
            }
            throw;
        }
    }

    private async Task<IUser> LoadProfileAsync(ulong userId)
    {
        var guildUser = await GetGuildUserAsync(userId);
        if (guildUser is not null)
            return guildUser;

        var discordClient = await discordClientProvider.GetClientAsync();
        return await discordClient.GetUserAsync(userId) ?? throw new InvalidOperationException($"Discord user {userId} was not found.");
    }

    private async Task<IGuild> GetGuildAsync()
    {
        await _guildLock.WaitAsync();
        try
        {
            if (_guild is null || _guildExpiresAt <= clock.GetUtcNow())
            {
                var discordClient = await discordClientProvider.GetClientAsync();
                var guild =
                    await discordClient.GetGuildAsync(options.GuildId)
                    ?? throw new InvalidOperationException("The configured Discord guild was not found.");
                _guild = guild;
                _guildExpiresAt = clock.GetUtcNow() + CacheLifetime;
            }
            return _guild;
        }
        finally
        {
            _guildLock.Release();
        }
    }

    public void Dispose() => _guildLock.Dispose();

    private sealed record CachedProfile(Task<IUser> User, DateTimeOffset ExpiresAt);
}
