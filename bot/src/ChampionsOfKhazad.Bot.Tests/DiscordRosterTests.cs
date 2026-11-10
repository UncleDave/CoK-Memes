using ChampionsOfKhazad.Bot.Roster;
using Discord;
using static ChampionsOfKhazad.Bot.Tests.DiscordConversationFixture;

namespace ChampionsOfKhazad.Bot.Tests;

public class DiscordRosterTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LeaderboardRegistrationFollowsFeatureFlagAndOnlyRemovesItsOwnSlashRegistration(bool enabled)
    {
        var created = new List<string>();
        var deleted = new List<string>();
        IApplicationCommand ApplicationCommand(ApplicationCommandType type) =>
            Stub<IApplicationCommand>(
                (method, _) =>
                    method.Name switch
                    {
                        "get_Name" => "leaderboard",
                        "get_Type" => type,
                        "DeleteAsync" => Delete(type),
                        _ => throw new NotSupportedException(method.Name),
                    }
            );
        Task Delete(ApplicationCommandType type)
        {
            deleted.Add(type.ToString());
            return Task.CompletedTask;
        }
        var guild = Stub<IGuild>(
            (method, args) =>
            {
                if (method.Name == "CreateApplicationCommandAsync")
                {
                    created.Add(((SlashCommandProperties)args![0]!).Name.Value);
                    return Task.FromResult(ApplicationCommand(ApplicationCommandType.Slash));
                }
                if (method.Name == "GetApplicationCommandsAsync")
                    return Task.FromResult<IReadOnlyCollection<IApplicationCommand>>([
                        ApplicationCommand(ApplicationCommandType.Slash),
                        ApplicationCommand(ApplicationCommandType.User),
                    ]);
                throw new NotSupportedException(method.Name);
            }
        );
        await SlashCommands.RegisterGuildCommandsAsync(guild, enabled);
        Assert.Equal(enabled, created.Contains("leaderboard"));
        Assert.Equal(enabled ? [] : new[] { "Slash" }, deleted);
    }

    [Theory]
    [InlineData(50UL, false)]
    [InlineData(51UL, false)]
    [InlineData(50UL, true)]
    [InlineData(51UL, true)]
    public async Task DestinationMustBeInConfiguredGuildAndAllMentionsAreDisabled(ulong channelGuild, bool celebration)
    {
        var sent = false;
        var batch = new RosterNoticeBatch("Mock notice", [], celebration ? "Guild honours" : null, celebration ? 0xF1C40Fu : null);
        var channel = Stub<ITextChannel>(
            (method, args) =>
            {
                if (method.Name == "get_GuildId")
                    return channelGuild;
                Assert.Equal("SendMessageAsync", method.Name);
                var mentionIndex = Array.FindIndex(method.GetParameters(), parameter => parameter.Name == "allowedMentions");
                Assert.Same(AllowedMentions.None, args![mentionIndex]);
                var textIndex = Array.FindIndex(method.GetParameters(), parameter => parameter.Name == "text");
                Assert.Equal(celebration ? null : batch.Text, args[textIndex]);
                var embedIndex = Array.FindIndex(method.GetParameters(), parameter => parameter.Name == "embed");
                if (celebration)
                {
                    var embed = Assert.IsType<Embed>(args[embedIndex], exactMatch: false);
                    Assert.Equal(batch.Title, embed.Title);
                    Assert.Equal(batch.Text, embed.Description);
                }
                else
                    Assert.Null(args[embedIndex]);
                sent = true;
                return Task.FromResult(
                    Stub<IUserMessage>((method, _) => method.Name == "get_Id" ? 123UL : throw new NotSupportedException(method.Name))
                );
            }
        );
        var client = Stub<IDiscordClient>(
            (method, _) => method.Name == "GetChannelAsync" ? Task.FromResult<IChannel>(channel) : throw new NotSupportedException(method.Name)
        );
        var sender = new DiscordRosterNotificationSender(new SharedDiscordRestClient(client));
        if (channelGuild == 50)
            Assert.Equal(123UL, await sender.SendAsync(50, 100, batch, TestContext.Current.CancellationToken));
        else
            await Assert.ThrowsAsync<InvalidOperationException>(() => sender.SendAsync(50, 100, batch, TestContext.Current.CancellationToken));
        Assert.Equal(channelGuild == 50, sent);
    }
}
