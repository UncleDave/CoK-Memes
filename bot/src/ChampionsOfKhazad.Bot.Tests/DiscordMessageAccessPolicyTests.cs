namespace ChampionsOfKhazad.Bot.Tests;

public class DiscordMessageAccessPolicyTests
{
    [Fact]
    public void GazetteCanReadAudienceSafeSourcesWhenNoRequestingGuildMemberIsCached()
    {
        var channels = new[]
        {
            new DiscordMessageAccessPolicy.ChannelCandidate(1, true, true, false, true),
            new DiscordMessageAccessPolicy.ChannelCandidate(2, false, false, false, true),
            new DiscordMessageAccessPolicy.ChannelCandidate(3, true, true, false, false),
        };
        Assert.Equal(1UL, Assert.Single(DiscordMessageAccessPolicy.GetGazetteSourceChannelIds(channels, true)));
    }

    [Theory]
    [InlineData(false, new ulong[] { 1, 2, 4 })]
    [InlineData(true, new ulong[] { 2, 4 })]
    public void GazetteUsesPublicationAudienceAndBotReadabilityWithoutRequesterCacheOrPermissions(bool everyone, ulong[] expected)
    {
        var channels = new[]
        {
            new DiscordMessageAccessPolicy.ChannelCandidate(1, true, false, true, true),
            new DiscordMessageAccessPolicy.ChannelCandidate(2, true, true, true, true),
            new DiscordMessageAccessPolicy.ChannelCandidate(3, false, true, true, true),
            new DiscordMessageAccessPolicy.ChannelCandidate(4, true, true, false, true),
            new DiscordMessageAccessPolicy.ChannelCandidate(5, true, true, true, false),
        };
        Assert.Equal(expected, DiscordMessageAccessPolicy.GetGazetteSourceChannelIds(channels, everyone).Order());
    }

    [Fact]
    public void BackgroundAccessRequiresNormalMemberAndBotReadabilityWithoutARequesterOrEveryoneAudience()
    {
        var channels = new[]
        {
            new DiscordMessageAccessPolicy.ChannelCandidate(1, true, false, false, true),
            new DiscordMessageAccessPolicy.ChannelCandidate(2, false, false, true, true),
            new DiscordMessageAccessPolicy.ChannelCandidate(3, true, true, true, false),
            new DiscordMessageAccessPolicy.ChannelCandidate(4, true, true, false, true),
        };

        var result = DiscordMessageAccessPolicy.GetBackgroundSourceChannelIds(channels);

        Assert.Equal(new ulong[] { 1, 4 }, result.Order());
        Assert.Equal(3UL, Assert.Single(DiscordMessageAccessPolicy.GetAllowedSourceChannelIds(channels, invokingChannelEveryoneCanRead: true)));
    }

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
