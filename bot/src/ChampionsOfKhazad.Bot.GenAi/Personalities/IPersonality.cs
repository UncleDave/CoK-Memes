using Microsoft.SemanticKernel.ChatCompletion;

namespace ChampionsOfKhazad.Bot.GenAi;

public interface IPersonality
{
    Task<string> InvokeAsync(ChatHistory chatHistory, ulong userId, CancellationToken cancellationToken = default);
    Task<string> InvokeAsync(
        ChatHistory chatHistory,
        ulong userId,
        IDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default
    );
}
