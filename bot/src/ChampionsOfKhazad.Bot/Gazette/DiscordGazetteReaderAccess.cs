using Discord;
using Discord.WebSocket;

namespace ChampionsOfKhazad.Bot;

internal sealed class DiscordGazetteReaderAccess(BotContextProvider contextProvider) : IGazetteReaderAccess
{
    public bool CanRead(IComponentInteraction interaction)
    {
        if (
            !contextProvider.IsReady
            || contextProvider.BotContext is not { Guild: SocketGuild guild } context
            || context.Client.ConnectionState != ConnectionState.Connected
            || !ReferenceEquals(context.Client.GetGuild(guild.Id), guild)
            || interaction.GuildId != guild.Id
            || interaction.ChannelId is not { } channelId
            || interaction.User is not SocketGuildUser viewer
            || viewer.Guild.Id != guild.Id
            || guild.GetTextChannel(channelId) is not { } channel
            || channel is SocketThreadChannel or SocketVoiceChannel
        )
            return false;
        return CanReadMessage(
            interaction.Message.Author.Id,
            context.BotId,
            interaction.Message.Channel.Id,
            channelId,
            viewer.GetPermissions(channel)
        );
    }

    internal static bool CanReadMessage(ulong authorId, ulong botId, ulong messageChannelId, ulong channelId, ChannelPermissions permissions) =>
        authorId == botId && messageChannelId == channelId && permissions is { ViewChannel: true, ReadMessageHistory: true };
}
