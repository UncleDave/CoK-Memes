using Discord;

namespace ChampionsOfKhazad.Bot.Tests;

public class SlashCommandRegistrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RegistrationCreatesEnabledCommandsAndRemovesOnlyTheRetiredRaidSlashCommand(bool hasRaidCommand)
    {
        var created = new List<string>();
        var deleted = new List<string>();
        IApplicationCommand Command(string name, ApplicationCommandType type = ApplicationCommandType.Slash) =>
            DiscordConversationFixture.Stub<IApplicationCommand>(
                (method, _) =>
                {
                    if (method.Name == "DeleteAsync")
                    {
                        deleted.Add(name);
                        return Task.CompletedTask;
                    }
                    return method.Name switch
                    {
                        "get_Name" => name,
                        "get_Type" => type,
                        _ => throw new NotSupportedException(method.Name),
                    };
                }
            );
        var existing = new List<IApplicationCommand> { Command("summarise"), Command("other"), Command("raids", ApplicationCommandType.User) };
        if (hasRaidCommand)
            existing.Add(Command("raids"));
        var guild = DiscordConversationFixture.Stub<IGuild>(
            (method, args) =>
            {
                if (method.Name == "CreateApplicationCommandAsync")
                {
                    var name = ((SlashCommandProperties)args![0]!).Name.Value;
                    created.Add(name);
                    return Task.FromResult(Command(name));
                }
                return method.Name == "GetApplicationCommandsAsync"
                    ? Task.FromResult<IReadOnlyCollection<IApplicationCommand>>(existing)
                    : throw new NotSupportedException(method.Name);
            }
        );

        await SlashCommands.RegisterGuildCommandsAsync(guild);

        Assert.Equal(["summarise"], created);
        Assert.Equal(hasRaidCommand ? ["raids"] : Array.Empty<string>(), deleted);
    }
}
