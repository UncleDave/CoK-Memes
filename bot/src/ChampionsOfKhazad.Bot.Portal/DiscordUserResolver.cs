using Discord;

namespace ChampionsOfKhazad.Bot.Portal;

public record DiscordUserResolverOptions(ulong GuildId);

public class DiscordUserResolver(IDiscordClientProvider discordClientProvider, DiscordUserResolverOptions options)
{
    public async Task<IGuildUser?> GetGuildUserAsync(ulong userId)
    {
        // Membership is authorization data: always read it afresh from the REST client.
        var discordClient = await discordClientProvider.GetClientAsync();
        var guild = await discordClient.GetGuildAsync(options.GuildId);
        return await guild.GetUserAsync(userId);
    }

    public async Task<IUser> GetUserAsync(ulong userId)
    {
        var guildUser = await GetGuildUserAsync(userId);
        if (guildUser is not null)
            return guildUser;

        var discordClient = await discordClientProvider.GetClientAsync();
        return await discordClient.GetUserAsync(userId);
    }
}
