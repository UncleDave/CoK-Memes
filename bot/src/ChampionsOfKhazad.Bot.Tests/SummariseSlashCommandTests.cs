using Discord;

namespace ChampionsOfKhazad.Bot.Tests;

public class SummariseSlashCommandTests
{
    [Fact]
    public void MessagesWithoutSummaryAreReturnedInChronologicalOrder()
    {
        var oldest = Message(1);
        var botMessageWithoutEmbeds = Message(2, DiscordConversationFixture.BotId);
        var botMessageWithMultipleEmbeds = Message(3, DiscordConversationFixture.BotId, Embed(), Embed("Unrelated footer"));
        var newest = Message(4);

        var messages = SummariseSlashCommand.GetMessagesSinceLastSummary(
            [newest, botMessageWithMultipleEmbeds, botMessageWithoutEmbeds, oldest],
            DiscordConversationFixture.BotId
        );

        Assert.Equal([oldest, botMessageWithoutEmbeds, botMessageWithMultipleEmbeds, newest], messages);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void SummaryInAnyEmbedExcludesTheSummaryAndOlderMessages(int summaryEmbedIndex)
    {
        var embeds = new[] { Embed(), Embed("Unrelated footer"), Embed() };
        embeds[summaryEmbedIndex] = Embed("Summarised for Raider");
        var summary = Message(2, DiscordConversationFixture.BotId, embeds);
        var newer = Message(3);
        var newest = Message(4);

        var messages = SummariseSlashCommand.GetMessagesSinceLastSummary([newest, newer, summary, Message(1)], DiscordConversationFixture.BotId);

        Assert.Equal([newer, newest], messages);
    }

    [Fact]
    public void SingleEmbedSummaryStillStopsMessageSelection()
    {
        var summary = Message(2, DiscordConversationFixture.BotId, Embed("Summarised for Raider"));
        var newest = Message(3);

        var messages = SummariseSlashCommand.GetMessagesSinceLastSummary([newest, summary, Message(1)], DiscordConversationFixture.BotId);

        Assert.Equal([newest], messages);
    }

    [Fact]
    public void SummaryFooterFromAnotherAuthorDoesNotStopMessageSelection()
    {
        var oldest = Message(1);
        var otherAuthorsSummary = Message(2, 42, Embed("Summarised for Raider"));
        var newest = Message(3);

        var messages = SummariseSlashCommand.GetMessagesSinceLastSummary([newest, otherAuthorsSummary, oldest], DiscordConversationFixture.BotId);

        Assert.Equal([oldest, otherAuthorsSummary, newest], messages);
    }

    [Fact]
    public void MostRecentSummaryIsTheBoundary()
    {
        var olderSummary = Message(1, DiscordConversationFixture.BotId, Embed("Summarised for Raider"));
        var newerSummary = Message(3, DiscordConversationFixture.BotId, Embed("Summarised for Another raider"));
        var newest = Message(4);

        var messages = SummariseSlashCommand.GetMessagesSinceLastSummary(
            [newest, newerSummary, Message(2), olderSummary],
            DiscordConversationFixture.BotId
        );

        Assert.Equal([newest], messages);
    }

    private static Embed Embed(string? footer = null)
    {
        var builder = new EmbedBuilder().WithDescription("An embed");
        if (footer is not null)
            builder.WithFooter(footer);
        return builder.Build();
    }

    private static IMessage Message(ulong id, ulong authorId = 2, params Embed[] embeds)
    {
        var author = DiscordConversationFixture.Stub<IUser>(
            (method, _) => method.Name == "get_Id" ? authorId : throw new NotSupportedException(method.Name)
        );
        return DiscordConversationFixture.Stub<IMessage>(
            (method, _) =>
                method.Name switch
                {
                    "get_Id" => id,
                    "get_Author" => author,
                    "get_Embeds" => embeds,
                    _ => throw new NotSupportedException(method.Name),
                }
        );
    }
}
