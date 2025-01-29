using Discord;

namespace ChampionsOfKhazad.Bot;

public static class SlashCommands
{
    public static readonly SlashCommand Raids = new(
        new SlashCommandBuilder()
            .WithName("raids")
            .WithDescription("Clear the Sunday/Monday sign up channels and create new raid sign ups")
            .WithDefaultMemberPermissions(GuildPermission.Administrator)
            .Build(),
        command => new RaidsSlashCommandExecuted(command)
    );

    public static readonly SlashCommand Suggest = new(
        new SlashCommandBuilder()
            .WithName("suggest")
            .WithDescription("Suggest a feature")
            .AddOption("suggestion", ApplicationCommandOptionType.String, "Your suggestion", true)
            .Build(),
        command => new SuggestSlashCommandExecuted(command)
    );

    public static readonly SlashCommand Rip = new(
        new SlashCommandBuilder()
            .WithName("rip")
            .WithDescription("Announce the untimely demise of your hardcore character")
            .AddOption("character", ApplicationCommandOptionType.String, "Your character's name", true)
            .AddOption("level", ApplicationCommandOptionType.Integer, "Your character's level", true, minValue: 1, maxValue: 60)
            .AddOption(
                new SlashCommandOptionBuilder()
                    .WithName("race")
                    .WithType(ApplicationCommandOptionType.String)
                    .WithDescription("Your character's race")
                    .WithRequired(true)
                    .AddChoice("Human")
                    .AddChoice("Dwarf")
                    .AddChoice("Gnome")
                    .AddChoice("Night Elf")
                    .AddChoice("Orc")
                    .AddChoice("Troll")
                    .AddChoice("Tauren")
                    .AddChoice("Undead")
            )
            .AddOption(
                new SlashCommandOptionBuilder()
                    .WithName("class")
                    .WithType(ApplicationCommandOptionType.String)
                    .WithDescription("Your character's class")
                    .WithRequired(true)
                    .AddChoice("Druid")
                    .AddChoice("Hunter")
                    .AddChoice("Mage")
                    .AddChoice("Paladin")
                    .AddChoice("Priest")
                    .AddChoice("Rogue")
                    .AddChoice("Shaman")
                    .AddChoice("Warlock")
                    .AddChoice("Warrior")
            )
            .AddOption("cause", ApplicationCommandOptionType.String, "The cause of death", true)
            .Build(),
        command => new RipSlashCommandExecuted(command)
    );

    public static readonly SlashCommand Summarise = new(
        new SlashCommandBuilder().WithName("summarise").WithDescription("Summarise the last 50 messages in this channel").Build(),
        command => new SummariseSlashCommandExecuted(command)
    );

    public static readonly SlashCommand[] GuildCommands = [Raids, Rip, Summarise];

    public static readonly SlashCommand[] GlobalCommands = [Suggest];

    public static readonly SlashCommand[] All = GuildCommands.Concat(GlobalCommands).ToArray();
}
