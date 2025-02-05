namespace ChampionsOfKhazad.Bot.DiscordMemes.WordOfTheDay;

public record WordOfTheDay(string Word, DateOnly Date, ulong? WinnerId = null)
{
    public override string ToString() => Word;
}
