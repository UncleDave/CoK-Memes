using Microsoft.SemanticKernel.ChatCompletion;

namespace ChampionsOfKhazad.Bot.GenAi;

public interface IPersonality
{
    Task<string> InvokeAsync(ChatHistory chatHistory, IMessageContext messageContext, CancellationToken cancellationToken = default);
    Task<string> InvokeAsync(
        ChatHistory chatHistory,
        IMessageContext messageContext,
        IDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default
    );
}
