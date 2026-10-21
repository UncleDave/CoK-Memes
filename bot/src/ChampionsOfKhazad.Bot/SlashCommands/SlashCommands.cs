using Discord;

namespace ChampionsOfKhazad.Bot;

public static class SlashCommands
{
    private static readonly string[] RaidAcknowledgements = ["More work?", "Right-o.", "Yes, milord.", "All right.", "Off I go, then!"];

    public static readonly SlashCommand Raids = new(
        new SlashCommandBuilder()
            .WithName("raids")
            .WithDescription("Clear the Sunday/Monday sign up channels and create new raid sign ups")
            .WithDefaultMemberPermissions(GuildPermission.Administrator)
            .Build(),
        command => new RaidsSlashCommandExecuted(command),
        command => command.RespondAsync(RandomUtils.PickRandom(RaidAcknowledgements), ephemeral: true)
    );

    public static readonly SlashCommand Suggest = new(
        new SlashCommandBuilder()
            .WithName("suggest")
            .WithDescription("Suggest a feature")
            .AddOption("suggestion", ApplicationCommandOptionType.String, "Your suggestion", true)
            .Build(),
        command => new SuggestSlashCommandExecuted(command),
        command => command.RespondAsync("Thanks for your suggestion!", ephemeral: true)
    );

    public static readonly SlashCommand Summarise = new(
        new SlashCommandBuilder().WithName("summarise").WithDescription("Summarise the last 50 messages in this channel").Build(),
        command => new SummariseSlashCommandExecuted(command),
        command => command.DeferAsync()
    );

    public static readonly SlashCommand[] GuildCommands = [Summarise];

    public static readonly SlashCommand[] GlobalCommands = [Suggest];

    public static readonly SlashCommand[] All = GuildCommands.Concat(GlobalCommands).ToArray();

    internal static async Task RegisterGuildCommandsAsync(IGuild guild)
    {
        foreach (var command in GuildCommands)
            await guild.CreateApplicationCommandAsync(command.Properties);

        // Creating enabled commands does not remove existing Discord registrations.
        // Retain /raids locally, but remove its remote registration until explicitly re-enabled.
        if (!GuildCommands.Contains(Raids))
        {
            foreach (var command in await guild.GetApplicationCommandsAsync())
            {
                if (command.Name == Raids.Properties.Name.Value && command.Type == ApplicationCommandType.Slash)
                    await command.DeleteAsync();
            }
        }
    }
}
