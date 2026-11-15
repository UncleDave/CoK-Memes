using System.Text.Json;
using ChampionsOfKhazad.Bot.GenAi;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot.Tests;

public class DiscordPublicationAudienceTests
{
    [Theory]
    [InlineData("Production", 1567646266689851402UL)]
    [InlineData("Development", 1092927596938924054UL)]
    public void SpecialsDestinationUsesTheConfiguredPublicationChannel(string environment, ulong expectedDestination)
    {
        using var config = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, $"appsettings.{environment}.json")));
        var root = config.RootElement;
        var destination = root.GetProperty("Specials").GetProperty("DestinationChannelId").GetUInt64();
        Assert.Equal(expectedDestination, destination);
        Assert.False(root.TryGetProperty("Gazette", out _));
    }

    [Theory]
    [InlineData("content")]
    [InlineData("view")]
    [InlineData("author")]
    [InlineData("timestamp")]
    [InlineData("url")]
    [InlineData("deleted")]
    public void EvidenceVerificationRejectsChangesAndMissingMessages(string change)
    {
        var source = new NotebookSource("https://discord.com/channels/1/3/42", 9, "Member", DateTime.UtcNow, "Announcement")
        {
            ContentHash = "raw",
            ViewHash = "view",
        };
        var changed = change switch
        {
            "content" => source with { ContentHash = "edit" },
            "view" => source with { ViewHash = "edit" },
            "author" => source with { AuthorId = 10 },
            "timestamp" => source with { TimestampUtc = source.TimestampUtc.AddSeconds(1) },
            "url" => source with { Url = "other" },
            _ => source,
        };
        Assert.True(DiscordPublicationAudience.SourcesMatch([source], [source]));
        Assert.False(DiscordPublicationAudience.SourcesMatch([source], change == "deleted" ? null : [changed]));
    }

    [Fact]
    public void DisconnectedAudienceFailsClosedWithAnActionableReason()
    {
        var audience = new DiscordPublicationAudience(
            new(),
            null!,
            Options.Create(new SpecialsOptions()),
            Options.Create(new DiscordMessageToolsOptions())
        );
        Assert.Null(audience.GetDestination());
        Assert.Contains("not ready", audience.DestinationError);
        Assert.Empty(audience.GetSourceChannels(1));
    }
}
