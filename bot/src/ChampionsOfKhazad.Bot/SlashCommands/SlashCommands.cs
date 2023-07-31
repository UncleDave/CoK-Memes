using Discord;

namespace ChampionsOfKhazad.Bot;

public static class SlashCommands
{
    public static readonly SlashCommand Raids =
        new(
            typeof(RaidsSlashCommand),
            new SlashCommandBuilder()
                .WithName("raids")
                .WithDescription("Clear the Sunday/Monday sign up channels and create new raid sign ups")
                .WithDefaultMemberPermissions(GuildPermission.Administrator)
                .Build()
        );

    public static readonly SlashCommand[] All = { Raids };
}
