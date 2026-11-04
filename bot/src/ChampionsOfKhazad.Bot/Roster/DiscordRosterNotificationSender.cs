using ChampionsOfKhazad.Bot.Roster;
using Discord;

namespace ChampionsOfKhazad.Bot;

public class DiscordRosterNotificationSender(SharedDiscordRestClient rest) : IRosterNotificationSender
{
    public async Task<ulong> SendAsync(ulong guildId, ulong channelId, string text, CancellationToken cancellationToken)
    {
        var request = new RequestOptions { CancelToken = cancellationToken };
        if (await rest.Client.GetChannelAsync(channelId, options: request) is not ITextChannel channel || channel.GuildId != guildId)
            throw new InvalidOperationException("Milestone destination must be a text channel in the configured guild.");
        return (await channel.SendMessageAsync(text, allowedMentions: AllowedMentions.None, options: request)).Id;
    }
}
