using Discord;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Internal;

namespace ChampionsOfKhazad.Bot.Portal;

public record DiscordUserResolverOptions(ulong GuildId);

public class DiscordUserResolver(IDiscordClientProvider discordClientProvider, DiscordUserResolverOptions options, TimeProvider clock) : IDisposable
{
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(15);
    private readonly Lock _cacheLock = new();
    private readonly MemoryCache _profiles = new(new MemoryCacheOptions { SizeLimit = 500, Clock = new CacheClock(clock) });
    private readonly MemoryCache _guildMetadata = new(new MemoryCacheOptions { SizeLimit = 1, Clock = new CacheClock(clock) });

    public async Task<IGuildUser?> GetGuildUserAsync(ulong userId)
    {
        // Only guild metadata is cached here. Authorization always reads membership afresh.
        var guild = await GetGuildAsync();
        return await guild.GetUserAsync(userId);
    }

    public Task<IUser> GetUserAsync(ulong userId) => GetCachedAsync(_profiles, userId, () => LoadProfileAsync(userId));

    private async Task<IUser> LoadProfileAsync(ulong userId)
    {
        var guildUser = await GetGuildUserAsync(userId);
        if (guildUser is not null)
            return guildUser;

        var discordClient = await discordClientProvider.GetClientAsync();
        return await discordClient.GetUserAsync(userId) ?? throw new InvalidOperationException($"Discord user {userId} was not found.");
    }

    private Task<IGuild> GetGuildAsync() =>
        GetCachedAsync(
            _guildMetadata,
            options.GuildId,
            async () =>
            {
                var discordClient = await discordClientProvider.GetClientAsync();
                return await discordClient.GetGuildAsync(options.GuildId)
                    ?? throw new InvalidOperationException("The configured Discord guild was not found.");
            }
        );

    private async Task<T> GetCachedAsync<T>(MemoryCache cache, object key, Func<Task<T>> load)
    {
        Lazy<Task<T>> value;
        lock (_cacheLock)
        {
            if (!cache.TryGetValue(key, out value!))
            {
                // Lazy starts the shared fetch outside the lock; MemoryCache owns expiry and eviction.
                value = new Lazy<Task<T>>(load);
                cache.Set(key, value, new MemoryCacheEntryOptions { Size = 1, AbsoluteExpirationRelativeToNow = CacheLifetime });
            }
        }

        try
        {
            return await value.Value;
        }
        catch
        {
            lock (_cacheLock)
            {
                if (cache.TryGetValue(key, out Lazy<Task<T>>? current) && ReferenceEquals(current, value))
                    cache.Remove(key);
            }
            throw;
        }
    }

    public void Dispose()
    {
        _profiles.Dispose();
        _guildMetadata.Dispose();
    }

    private sealed class CacheClock(TimeProvider timeProvider) : ISystemClock
    {
        public DateTimeOffset UtcNow => timeProvider.GetUtcNow();
    }
}
