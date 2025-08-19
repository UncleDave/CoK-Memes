using Microsoft.SemanticKernel.ChatCompletion;

namespace ChampionsOfKhazad.Bot.GenAi;

public interface IPersonality
{
    Task<string> InvokeAsync(ChatHistory chatHistory, CancellationToken cancellationToken = default);
    Task<string> InvokeAsync(
        ChatHistory chatHistory,
        IDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default
    );
}
