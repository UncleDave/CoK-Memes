using ChampionsOfKhazad.Bot.Roster;
using Discord;

namespace ChampionsOfKhazad.Bot;

public class DiscordRosterNotificationSender(SharedDiscordRestClient rest) : IRosterNotificationSender
{
    public async Task<ulong> SendAsync(ulong guildId, ulong channelId, RosterNoticeBatch batch, CancellationToken cancellationToken)
    {
        var request = new RequestOptions { CancelToken = cancellationToken };
        if (await rest.Client.GetChannelAsync(channelId, options: request) is not ITextChannel channel || channel.GuildId != guildId)
            throw new InvalidOperationException("Milestone destination must be a text channel in the configured guild.");
        return (
            await channel.SendMessageAsync(
                batch.Title is null ? batch.Text : null,
                embed: CreateEmbed(batch),
                allowedMentions: AllowedMentions.None,
                options: request
            )
        ).Id;
    }

    internal static Embed? CreateEmbed(RosterNoticeBatch batch) =>
        batch.Title is null
            ? null
            : new EmbedBuilder().WithTitle(batch.Title).WithDescription(batch.Text).WithColor(new Color(batch.Colour ?? 0x2ECC71)).Build();
}
