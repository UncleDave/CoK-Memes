namespace ChampionsOfKhazad.Bot;

internal static class DiscordMessageAccessPolicy
{
    public static IReadOnlySet<ulong> GetAllowedSourceChannelIds(IEnumerable<ChannelCandidate> channels, ChannelCandidate invokingChannel)
    {
        if (!invokingChannel.RequesterCanRead)
            return new HashSet<ulong>();

        return channels
            .Where(channel => channel.NormalUserCanRead && channel.RequesterCanRead)
            .Where(channel => !invokingChannel.EveryoneCanRead || channel.EveryoneCanRead)
            .Select(channel => channel.Id)
            .ToHashSet();
    }

    internal sealed record ChannelCandidate(ulong Id, bool NormalUserCanRead, bool EveryoneCanRead, bool RequesterCanRead);
}
