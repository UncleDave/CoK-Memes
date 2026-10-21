using Discord;

namespace ChampionsOfKhazad.Bot.Tests;

public class SlashCommandAcknowledgementTests
{
    [Theory]
    [InlineData("raids", "RespondAsync", true)]
    [InlineData("suggest", "RespondAsync", true)]
    [InlineData("summarise", "DeferAsync", false)]
    public async Task EveryCommandDefinesOneInitialAcknowledgementWithItsOriginalVisibility(string name, string expectedMethod, bool ephemeral)
    {
        var slashCommand = SlashCommands.All.Append(SlashCommands.Raids).Single(x => x.Properties.Name.Value == name);
        var calls = new List<string>();
        var command = DiscordConversationFixture.Stub<ISlashCommandInteraction>(
            (method, args) =>
            {
                calls.Add(method.Name);
                Assert.Equal(expectedMethod, method.Name);
                var ephemeralIndex = Array.FindIndex(method.GetParameters(), parameter => parameter.Name == "ephemeral");
                Assert.Equal(ephemeral, args![ephemeralIndex]);
                if (name == "suggest")
                    Assert.Equal("Thanks for your suggestion!", args[0]);
                return Task.CompletedTask;
            }
        );

        await slashCommand.AcknowledgeAsync(command);

        Assert.Equal([expectedMethod], calls);
    }

    [Fact]
    public void HardcoreDeathCommandIsNotRegistered()
    {
        Assert.DoesNotContain(SlashCommands.All, command => command.Properties.Name.Value == "rip");
    }

    [Fact]
    public void RaidCommandIsRetainedButNotRegistered()
    {
        Assert.Equal("raids", SlashCommands.Raids.Properties.Name.Value);
        Assert.DoesNotContain(SlashCommands.All, command => command.Properties.Name.Value == "raids");
    }
}
