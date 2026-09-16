namespace ChampionsOfKhazad.Bot.Tests;

public class DiscordMessageAccessPolicyTests
{
    [Fact]
    public void OfficerOnlyChannelsAreExcluded()
    {
        var channels = new[]
        {
            Channel(1, normalUserCanRead: true, everyoneCanRead: false, requesterCanRead: true),
            Channel(2, normalUserCanRead: false, everyoneCanRead: false, requesterCanRead: true),
        };

        var result = DiscordMessageAccessPolicy.GetAllowedSourceChannelIds(channels, invokingChannelEveryoneCanRead: false);

        Assert.Contains(1UL, result);
        Assert.DoesNotContain(2UL, result);
    }

    [Fact]
    public void ChannelsHiddenFromRequesterAreExcluded()
    {
        var channels = new[]
        {
            Channel(1, normalUserCanRead: true, everyoneCanRead: false, requesterCanRead: true),
            Channel(2, normalUserCanRead: true, everyoneCanRead: false, requesterCanRead: false),
        };

        var result = DiscordMessageAccessPolicy.GetAllowedSourceChannelIds(channels, invokingChannelEveryoneCanRead: false);

        Assert.Contains(1UL, result);
        Assert.DoesNotContain(2UL, result);
    }

    [Fact]
    public void PrivateInvocationOutsideNormalRoleCanReadNormalRoleChannels()
    {
        var channels = new[] { Channel(1, normalUserCanRead: true, everyoneCanRead: false, requesterCanRead: true) };

        var result = DiscordMessageAccessPolicy.GetAllowedSourceChannelIds(channels, invokingChannelEveryoneCanRead: false);

        Assert.Contains(1UL, result);
    }

    [Fact]
    public void EveryoneVisibleInvocationCannotExposeNormalRoleOnlyChannel()
    {
        var channels = new[]
        {
            Channel(1, normalUserCanRead: true, everyoneCanRead: true, requesterCanRead: true),
            Channel(2, normalUserCanRead: true, everyoneCanRead: false, requesterCanRead: true),
        };

        var result = DiscordMessageAccessPolicy.GetAllowedSourceChannelIds(channels, invokingChannelEveryoneCanRead: true);

        Assert.Contains(1UL, result);
        Assert.DoesNotContain(2UL, result);
    }

    [Fact]
    public void NormalRoleOnlyInvocationCanReadEveryoneAndNormalRoleChannels()
    {
        var channels = new[]
        {
            Channel(1, normalUserCanRead: true, everyoneCanRead: false, requesterCanRead: true),
            Channel(2, normalUserCanRead: true, everyoneCanRead: true, requesterCanRead: true),
        };

        var result = DiscordMessageAccessPolicy.GetAllowedSourceChannelIds(channels, invokingChannelEveryoneCanRead: false);

        Assert.Contains(1UL, result);
        Assert.Contains(2UL, result);
    }

    private static DiscordMessageAccessPolicy.ChannelCandidate Channel(
        ulong id,
        bool normalUserCanRead,
        bool everyoneCanRead,
        bool requesterCanRead
    ) => new(id, normalUserCanRead, everyoneCanRead, requesterCanRead);
}
