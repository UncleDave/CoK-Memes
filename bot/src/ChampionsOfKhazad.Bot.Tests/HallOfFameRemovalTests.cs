using System.Text.Json;

namespace ChampionsOfKhazad.Bot.Tests;

public class HallOfFameRemovalTests
{
    [Theory]
    [InlineData("appsettings.json")]
    [InlineData("appsettings.Development.json")]
    [InlineData("appsettings.Production.json")]
    public void ConfigurationDoesNotIncludeHallOfFame(string fileName)
    {
        using var config = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, fileName)));

        Assert.False(config.RootElement.GetProperty("EventHandlers").TryGetProperty("HallOfFame", out _));
    }

    [Theory]
    [InlineData("HallOfFameReactionHandler")]
    [InlineData("HallOfFameReactionHandlerOptions")]
    [InlineData("ReactionAdded")]
    public void BotDoesNotIncludeHallOfFameOrItsUnusedReactionNotification(string typeName)
    {
        Assert.Null(typeof(BotService).Assembly.GetType($"ChampionsOfKhazad.Bot.{typeName}"));
    }
}
