using Discord;

namespace ChampionsOfKhazad.Bot.Tests;

public class MessageExtensionsTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(DiscordConfig.MaxMessageSize)]
    public void ContentWithinLimitRemainsInOneChunk(int length)
    {
        var content = new string('a', length);

        var chunks = MessageExtensions.SplitMessageContent(content);

        Assert.Equal([content], chunks);
    }

    [Fact]
    public void ContentOverLimitSplitsAtParagraphBoundary()
    {
        var firstParagraph = new string('a', 1_500) + "\n\n";
        var content = firstParagraph + new string('b', 600);

        var chunks = MessageExtensions.SplitMessageContent(content);

        Assert.Equal([firstParagraph, new string('b', 600)], chunks);
    }

    [Fact]
    public void LongUnbrokenContentUsesHardLimit()
    {
        var content = new string('a', DiscordConfig.MaxMessageSize + 1);

        var chunks = MessageExtensions.SplitMessageContent(content);

        Assert.Equal(2, chunks.Count);
        Assert.Equal(DiscordConfig.MaxMessageSize, chunks[0].Length);
        Assert.Equal("a", chunks[1]);
    }

    [Fact]
    public void HardSplitDoesNotSeparateSurrogatePair()
    {
        var emoji = "😀";
        var content = new string('a', DiscordConfig.MaxMessageSize - 1) + emoji;

        var chunks = MessageExtensions.SplitMessageContent(content);

        Assert.Equal([new string('a', DiscordConfig.MaxMessageSize - 1), emoji], chunks);
    }

    [Fact]
    public void EveryChunkRespectsDiscordLimitAndPreservesContent()
    {
        var content = string.Join(' ', Enumerable.Repeat(new string('a', 100), 50));

        var chunks = MessageExtensions.SplitMessageContent(content);

        Assert.All(chunks, chunk => Assert.InRange(chunk.Length, 1, DiscordConfig.MaxMessageSize));
        Assert.Equal(content, string.Concat(chunks));
    }
}
