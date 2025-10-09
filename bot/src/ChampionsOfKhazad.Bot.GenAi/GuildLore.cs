namespace ChampionsOfKhazad.Bot.GenAi;

public record GuildLore(string Name, string Content) : Lore(Name)
{
    public override string ToString() => Content;
}
