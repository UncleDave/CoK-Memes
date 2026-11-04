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

    public static readonly SlashCommand Leaderboard = new(
        new SlashCommandBuilder()
            .WithName("leaderboard")
            .WithDescription("Show guild character or profession ranks")
            .AddOption("profession", ApplicationCommandOptionType.String, "Profession name or skill-line ID")
            .AddOption(
                new SlashCommandOptionBuilder()
                    .WithName("page")
                    .WithDescription("Page number")
                    .WithType(ApplicationCommandOptionType.Integer)
                    .WithMinValue(1)
            )
            .Build(),
        command => new LeaderboardSlashCommandExecuted(command),
        command => command.DeferAsync()
    );

    public static readonly SlashCommand[] GlobalCommands = [Suggest];

    public static readonly SlashCommand[] All = GuildCommands.Concat(GlobalCommands).Append(Leaderboard).ToArray();

    internal static async Task RegisterGuildCommandsAsync(IGuild guild, bool rosterEnabled = false)
    {
        foreach (var command in rosterEnabled ? GuildCommands.Append(Leaderboard) : GuildCommands)
            await guild.CreateApplicationCommandAsync(command.Properties);

        // Creating enabled commands does not remove existing Discord registrations.
        // Retain retired/optional handlers locally, but remove their own disabled slash registrations.
        if (!GuildCommands.Contains(Raids) || !rosterEnabled)
        {
            foreach (var command in await guild.GetApplicationCommandsAsync())
            {
                if (!GuildCommands.Contains(Raids) && command.Name == Raids.Properties.Name.Value && command.Type == ApplicationCommandType.Slash)
                    await command.DeleteAsync();
                if (!rosterEnabled && command.Name == Leaderboard.Properties.Name.Value && command.Type == ApplicationCommandType.Slash)
                    await command.DeleteAsync();
            }
        }
    }
}
