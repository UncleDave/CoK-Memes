namespace ChampionsOfKhazad.Bot.GenAi;

public interface IPersonality
{
    Task<string> InvokeAsync(ChatHistory chatHistory, IMessageContext messageContext, CancellationToken cancellationToken = default);
}
