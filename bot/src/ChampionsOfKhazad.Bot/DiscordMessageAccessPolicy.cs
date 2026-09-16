namespace ChampionsOfKhazad.Bot;

internal static class DiscordMessageAccessPolicy
{
    public static IReadOnlySet<ulong> GetAllowedSourceChannelIds(IEnumerable<ChannelCandidate> channels, ulong invokingChannelId)
    {
        var accessibleChannels = channels.Where(channel => channel.NormalUserCanRead && channel.RequesterCanRead).ToArray();
        var invokingChannel = accessibleChannels.SingleOrDefault(channel => channel.Id == invokingChannelId);

        if (invokingChannel is null)
            return new HashSet<ulong>();

        return accessibleChannels
            .Where(channel => !invokingChannel.EveryoneCanRead || channel.EveryoneCanRead)
            .Select(channel => channel.Id)
            .ToHashSet();
    }

    internal sealed record ChannelCandidate(ulong Id, bool NormalUserCanRead, bool EveryoneCanRead, bool RequesterCanRead);
}
