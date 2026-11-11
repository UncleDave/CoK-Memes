using Discord;
using static ChampionsOfKhazad.Bot.Tests.DiscordConversationFixture;

namespace ChampionsOfKhazad.Bot.Tests;

public class SpecialsPublicationTests
{
    [Fact]
    public async Task PublicationSendsExactlyOnePlainMenuWithoutMentionsEmbedsOrComponents()
    {
        const string menu = "**Weekly specials**\n@everyone supper";
        var calls = 0;
        var message = Stub<IUserMessage>((method, _) => method.Name == "get_Id" ? 123UL : throw new NotSupportedException(method.Name));
        var channel = Stub<ITextChannel>(
            (method, arguments) =>
            {
                Assert.Equal("SendMessageAsync", method.Name);
                calls++;
                var parameters = method.GetParameters();
                object? Argument(string name) => arguments![Array.FindIndex(parameters, parameter => parameter.Name == name)];
                Assert.Equal(menu, Argument("text"));
                Assert.Same(AllowedMentions.None, Argument("allowedMentions"));
                Assert.Null(Argument("embed"));
                Assert.Null(Argument("embeds"));
                Assert.Null(Argument("components"));
                Assert.Equal(TestContext.Current.CancellationToken, Assert.IsType<RequestOptions>(Argument("options")).CancelToken);
                return Task.FromResult(message);
            }
        );
        Assert.Equal(123UL, await DiscordSpecialsGateway.SendMenuAsync(channel, menu, () => true, TestContext.Current.CancellationToken));
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(false, 10)]
    [InlineData(true, 2001)]
    [InlineData(true, 0)]
    public async Task RevokedPublicationOrInvalidMenuDoesNotSend(bool access, int length)
    {
        var channel = Stub<ITextChannel>((_, _) => throw new InvalidOperationException("Unexpected network call"));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DiscordSpecialsGateway.SendMenuAsync(channel, new string('x', length), () => access, TestContext.Current.CancellationToken)
        );
    }
}
