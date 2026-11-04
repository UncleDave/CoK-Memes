using ChampionsOfKhazad.Bot.Roster;
using ChampionsOfKhazad.Bot.Roster.Blizzard;
using Microsoft.Extensions.Configuration;

namespace ChampionsOfKhazad.Bot.Tests;

public class GuildRosterConfigurationTests
{
    [Theory]
    [InlineData("Development", 1092927596938924054UL, 735209372824764568UL)]
    [InlineData("Production", 1567646266689851402UL, 812311049914548234UL)]
    public async Task EnvironmentDestinationsUseLiveGuildDefaultsAndTheDisableSwitchStopsAllWork(string environment, ulong channel, ulong guild)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(AppContext.BaseDirectory, "RosterConfiguration"))
            .AddJsonFile("appsettings.json")
            .AddJsonFile($"appsettings.{environment}.json")
            .Build();
        var options = configuration.GetSection(GuildRosterOptions.Key).Get<GuildRosterOptions>()!;
        options.GuildId = configuration.GetValue<ulong>("Bot:GuildId");
        options.Validate();
        Assert.Equal(channel, options.MilestoneChannelId);
        Assert.Equal(guild, options.GuildId);
        Assert.True(options.Enabled);
        var blizzard = configuration.GetSection(BlizzardRosterOptions.Key).Get<BlizzardRosterOptions>()!;
        Assert.Equal("eu", blizzard.Region);
        Assert.Equal("Crossroads Mega Chippy", blizzard.GuildName);
        Assert.Equal("crossroads-mega-chippy", blizzard.GuildSlug);
        Assert.Null(blizzard.ClientId);
        Assert.Null(blizzard.ClientSecret);
        Assert.Null(configuration["GuildRoster:MockFile"]);

        options.Enabled = false;
        using var fixture = new RosterFixture();
        using var service = new GuildRosterService(options, fixture.Source, fixture.Store, fixture.Notices, fixture.Clock);
        Assert.False(service.NotificationsEnabled);
        await service.RefreshAsync(fixture.Token);
        await service.DeliverAsync(new UnexpectedSender(), fixture.Token);
        Assert.Equal(0, fixture.Source.RosterReads);
        Assert.Equal(0, fixture.Store.Reads);
        Assert.Empty(fixture.Notices.Items);
    }

    private sealed class UnexpectedSender : IRosterNotificationSender
    {
        public Task<ulong> SendAsync(ulong guildId, ulong channelId, string text, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Disabled roster configuration must not send a Discord message.");
    }
}
