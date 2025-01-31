using Microsoft.SemanticKernel.ChatCompletion;

namespace ChampionsOfKhazad.Bot.GenAi;

public interface ICompletionService
{
    IPersonality Lorekeeper { get; }
    IPersonality Sycophant { get; }
    IPersonality Contrarian { get; }
    IPersonality DisappointedTeacher { get; }
    IPersonality CondescendingTeacher { get; }
    IPersonality NoNutNovemberExpert { get; }
    IPersonality RatExpert { get; }
    IPersonality StonerBro { get; }
    IPersonality HarassmentLawyer { get; }
    IPersonality ProHarassmentLawyer { get; }

    public Task<string> InvokeAsync(ChatHistory chatHistory, CancellationToken cancellationToken = default);
}
