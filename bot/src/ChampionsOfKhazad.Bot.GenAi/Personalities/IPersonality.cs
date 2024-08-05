namespace ChampionsOfKhazad.Bot.GenAi;

public interface IPersonality
{
    Task<string> InvokeAsync(ChatMessage input, IEnumerable<ChatMessage> chatHistory, CancellationToken cancellationToken = default);
}
