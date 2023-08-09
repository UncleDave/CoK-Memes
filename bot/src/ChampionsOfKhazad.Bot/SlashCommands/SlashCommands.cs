using Discord;

namespace ChampionsOfKhazad.Bot;

public static class SlashCommands
{
    public static readonly SlashCommand Raids =
        new(
            new SlashCommandBuilder()
                .WithName("raids")
                .WithDescription("Clear the Sunday/Monday sign up channels and create new raid sign ups")
                .WithDefaultMemberPermissions(GuildPermission.Administrator)
                .Build(),
            command => new RaidsSlashCommandExecuted(command)
        );

    public static readonly SlashCommand Suggest =
        new(
            new SlashCommandBuilder()
                .WithName("suggest")
                .WithDescription("Suggest a feature")
                .AddOption("suggestion", ApplicationCommandOptionType.String, "Your suggestion", true)
                .Build(),
            command => new SuggestSlashCommandExecuted(command)
        );

    public static readonly SlashCommand[] GuildCommands = { Raids };

    public static readonly SlashCommand[] GlobalCommands = { Suggest };

    public static readonly SlashCommand[] All = GuildCommands.Concat(GlobalCommands).ToArray();
}
