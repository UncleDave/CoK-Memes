using Discord;

namespace ChampionsOfKhazad.Bot;

public static class SlashCommandOptionBuilderExtensions
{
    public static SlashCommandOptionBuilder AddChoice(this SlashCommandOptionBuilder builder, string value) => builder.AddChoice(value, value);
}
