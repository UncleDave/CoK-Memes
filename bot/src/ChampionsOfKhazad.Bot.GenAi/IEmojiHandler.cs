namespace ChampionsOfKhazad.Bot.GenAi;

public interface IEmojiHandler
{
    IEnumerable<string> GetEmojis();
    string ProcessMessage(string message);
}
