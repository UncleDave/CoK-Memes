using ChampionsOfKhazad.Bot.Lore.Abstractions;

namespace ChampionsOfKhazad.Bot.Lore;

public record GuildLore(string Name, string Content) : Lore(Name), IGuildLore
{
    public override string ToString() => Content;
}
