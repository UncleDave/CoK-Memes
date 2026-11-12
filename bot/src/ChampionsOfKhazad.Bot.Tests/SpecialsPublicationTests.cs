using ChampionsOfKhazad.Bot.GenAi;
using Discord;
using static ChampionsOfKhazad.Bot.Tests.DiscordConversationFixture;

namespace ChampionsOfKhazad.Bot.Tests;

public class SpecialsPublicationTests
{
    [Fact]
    public async Task PublicationSendsExactlyThePreviewEmbedWithoutMentionsTextOrComponents()
    {
        var menu = Render([Dish(0, "@everyone supper", "Same batter.")]);
        var calls = 0;
        var message = Stub<IUserMessage>((method, _) => method.Name == "get_Id" ? 123UL : throw new NotSupportedException(method.Name));
        var channel = Stub<ITextChannel>(
            (method, arguments) =>
            {
                Assert.Equal("SendMessageAsync", method.Name);
                calls++;
                var parameters = method.GetParameters();
                object? Argument(string name) => arguments![Array.FindIndex(parameters, parameter => parameter.Name == name)];
                Assert.Null(Argument("text"));
                Assert.Same(AllowedMentions.None, Argument("allowedMentions"));
                Assert.Same(menu, Argument("embed"));
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
    [InlineData(false)]
    [InlineData(true)]
    public async Task RevokedPublicationOrEmptyMenuDoesNotSend(bool access)
    {
        var channel = Stub<ITextChannel>((_, _) => throw new InvalidOperationException("Unexpected network call"));
        var menu = access ? new EmbedBuilder().Build() : Render([Dish(0)]);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DiscordSpecialsGateway.SendMenuAsync(channel, menu, () => access, TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public void MenuHasADatedGoldHeadingAndFullWidthDishEntriesWithoutPrivateDetailsOrFooter()
    {
        var menu = Render([Dish(0), Dish(1, "Committee Fish Supper", "Six forks, one chip.")]);
        Assert.Equal("🍟 Crossroads Mega Chippy", menu.Title);
        Assert.Equal("**Specials of the week**\n5–12 November 2026", menu.Description);
        Assert.Equal(Color.Gold, menu.Color);
        Assert.Collection(
            menu.Fields,
            field =>
            {
                Assert.Equal("New Sign Supper — 8 silver", field.Name);
                Assert.Equal("Same batter. Suspiciously fresh packaging.", field.Value);
                Assert.False(field.Inline);
            },
            field =>
            {
                Assert.Equal("Committee Fish Supper — 8 silver", field.Name);
                Assert.Equal("Six forks, one chip.", field.Value);
                Assert.False(field.Inline);
            }
        );
        Assert.Null(menu.Footer);
        Assert.Null(menu.Image);
        Assert.Null(menu.Thumbnail);
        Assert.Null(menu.Url);
    }

    [Fact]
    public void GeneratedMarkdownIsEscapedWithoutLosingTheDishOrPrice()
    {
        var dish = Assert.Single(Render([Dish(0, "*Fish* [supper]", "<@123> says _fresh_!")]).Fields);
        Assert.Equal("\\*Fish\\* \\[supper\\] — 8 silver", dish.Name);
        Assert.Equal("\\<@123\\> says \\_fresh\\_!", dish.Value);
    }

    [Fact]
    public async Task FiveLongDishesFitAnEmbedAndAreSentWholeEvenAboveThePlainTextLimit()
    {
        var dishes = Enumerable.Range(0, 5).Select(index => Dish(index, new string('*', 70), new string('_', 220))).ToArray();
        var menu = Render(dishes);
        Assert.True(menu.Fields.Sum(field => field.Name.Length + field.Value.Length) > 2000);
        Assert.Equal(5, menu.Fields.Length);
        Assert.All(menu.Fields, field => Assert.Equal(string.Concat(Enumerable.Repeat("\\_", 220)), field.Value));
        var message = Stub<IUserMessage>((method, _) => method.Name == "get_Id" ? 123UL : throw new NotSupportedException(method.Name));
        var channel = Stub<ITextChannel>(
            (method, arguments) =>
            {
                Assert.Equal("SendMessageAsync", method.Name);
                var embedIndex = Array.FindIndex(method.GetParameters(), parameter => parameter.Name == "embed");
                Assert.Same(menu, arguments![embedIndex]);
                return Task.FromResult(message);
            }
        );
        Assert.Equal(123UL, await DiscordSpecialsGateway.SendMenuAsync(channel, menu, () => true, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void EmptyOrOverfullMenusAreRejected(int count) =>
        Assert.Throws<SpecialsValidationException>(() => Render(Enumerable.Range(0, count).Select(index => Dish(index)).ToArray()));

    [Fact]
    public void RepeatedEventsCannotBecomeSeparateMenuEntries() => Assert.Throws<SpecialsValidationException>(() => Render([Dish(0), Dish(0)]));

    [Theory]
    [InlineData(250, 10)]
    [InlineData(10, 1025)]
    [InlineData(240, 950)]
    public void OversizedFieldsOrTotalEmbedAreRejectedWithoutTruncation(int nameLength, int descriptionLength)
    {
        var dishes = Enumerable.Range(0, 5).Select(index => Dish(index, new string('x', nameLength), new string('y', descriptionLength))).ToArray();
        var exception = Assert.Throws<SpecialsValidationException>(() => Render(dishes));
        Assert.Equal("MenuLength", exception.Category);
    }

    private static Embed Render(IReadOnlyList<SpecialsDish> dishes) =>
        SpecialsDirectMessageCommand.Render(dishes, new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero), new(2026, 11, 12, 12, 0, 0, TimeSpan.Zero));

    private static SpecialsDish Dish(int index, string name = "New Sign Supper", string description = "Same batter. Suspiciously fresh packaging.") =>
        new(index, name, description, "8 silver", "Private event summary", ["https://discord.com/channels/1/3/42"]);
}
