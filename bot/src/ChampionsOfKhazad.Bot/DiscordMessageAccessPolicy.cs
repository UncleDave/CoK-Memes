namespace ChampionsOfKhazad.Bot;

internal static class DiscordMessageAccessPolicy
{
    public static IReadOnlySet<ulong> GetAllowedSourceChannelIds(IEnumerable<ChannelCandidate> channels, bool invokingChannelEveryoneCanRead) =>
        channels
            .Where(channel => channel.NormalUserCanRead && channel.RequesterCanRead)
            .Where(channel => !invokingChannelEveryoneCanRead || channel.EveryoneCanRead)
            .Select(channel => channel.Id)
            .ToHashSet();

    public static IReadOnlySet<ulong> GetBackgroundSourceChannelIds(IEnumerable<ChannelCandidate> channels) =>
        channels.Where(channel => channel.NormalUserCanRead && channel.BotCanRead).Select(channel => channel.Id).ToHashSet();

    public static IReadOnlySet<ulong> GetGazetteSourceChannelIds(IEnumerable<ChannelCandidate> channels, bool destinationEveryoneCanRead) =>
        GetBackgroundSourceChannelIds(channels.Where(channel => !destinationEveryoneCanRead || channel.EveryoneCanRead));

    internal sealed record ChannelCandidate(ulong Id, bool NormalUserCanRead, bool EveryoneCanRead, bool RequesterCanRead, bool BotCanRead = false);
}
