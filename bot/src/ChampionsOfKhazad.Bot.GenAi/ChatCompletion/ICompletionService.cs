using Microsoft.Extensions.AI;

namespace ChampionsOfKhazad.Bot.GenAi;

public interface ICompletionService
{
    IPersonality Lorekeeper { get; }
    IPersonality Sycophant { get; }
    IPersonality Contrarian { get; }
    IPersonality DisappointedTeacher { get; }
    IPersonality CondescendingTeacher { get; }
    IPersonality RatExpert { get; }
    IPersonality StonerBro { get; }

    Task<string> InvokeAsync(ChatHistory chatHistory, CancellationToken cancellationToken = default);

    Task<string> InvokeAsync(ChatHistory chatHistory, ReasoningEffort reasoningEffort, CancellationToken cancellationToken = default);
}
