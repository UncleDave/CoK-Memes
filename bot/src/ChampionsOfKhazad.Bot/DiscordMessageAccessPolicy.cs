namespace ChampionsOfKhazad.Bot;

internal static class DiscordMessageAccessPolicy
{
    public static IReadOnlySet<ulong> GetAllowedSourceChannelIds(IEnumerable<ChannelCandidate> channels, bool invokingChannelEveryoneCanRead) =>
        channels
            .Where(channel => channel.NormalUserCanRead && channel.RequesterCanRead)
            .Where(channel => !invokingChannelEveryoneCanRead || channel.EveryoneCanRead)
            .Select(channel => channel.Id)
            .ToHashSet();

    internal sealed record ChannelCandidate(ulong Id, bool NormalUserCanRead, bool EveryoneCanRead, bool RequesterCanRead);
}
