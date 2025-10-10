namespace ChampionsOfKhazad.Bot.Lore.Abstractions;

public record GuildLore(string Name, string Content) : Lore(Name)
{
    public override string ToString() => Content;
}
