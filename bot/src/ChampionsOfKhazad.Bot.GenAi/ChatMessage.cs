namespace ChampionsOfKhazad.Bot.GenAi;

public record ChatMessage(string UserName, string Message)
{
    public override string ToString() => $"{UserName}: {Message}";
}
