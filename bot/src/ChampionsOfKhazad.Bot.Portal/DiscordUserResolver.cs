using Discord;

namespace ChampionsOfKhazad.Bot.Portal;

public record DiscordUserResolverOptions(ulong GuildId);

public class DiscordUserResolver(DiscordClientProvider discordClientProvider, DiscordUserResolverOptions options)
{
    private readonly Dictionary<ulong, IUser> _userCache = new();

    public async Task<IUser> GetUserAsync(ulong userId)
    {
        if (_userCache.TryGetValue(userId, out var cachedUser))
        {
            return cachedUser;
        }

        var discordClient = await discordClientProvider.GetClientAsync();
        var guild = await discordClient.GetGuildAsync(options.GuildId);
        var user = await guild.GetUserAsync(userId) ?? await discordClient.GetUserAsync(userId);

        _userCache.Add(userId, user);

        return user;
    }
}
