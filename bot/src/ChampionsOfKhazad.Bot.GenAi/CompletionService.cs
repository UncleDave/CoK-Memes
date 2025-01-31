using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace ChampionsOfKhazad.Bot.GenAi;

internal class CompletionService(
    Kernel kernel,
    IChatCompletionService chatCompletionService,
    LorekeeperPersonality lorekeeperPersonality,
    SycophantPersonality sycophantPersonality,
    ContrarianPersonality contrarianPersonality,
    DisappointedTeacherPersonality disappointedTeacherPersonality,
    CondescendingTeacherPersonality condescendingTeacherPersonality,
    NoNutNovemberExpertPersonality noNutNovemberExpertPersonality,
    RatExpertPersonality ratExpertPersonality,
    StonerBroPersonality stonerBroPersonality,
    HarassmentLawyerPersonality harassmentLawyerPersonality,
    ProHarassmentLawyerPersonality proHarassmentLawyerPersonality
) : ICompletionService
{
    public IPersonality Lorekeeper => lorekeeperPersonality;
    public IPersonality Sycophant => sycophantPersonality;
    public IPersonality Contrarian => contrarianPersonality;
    public IPersonality DisappointedTeacher => disappointedTeacherPersonality;
    public IPersonality CondescendingTeacher => condescendingTeacherPersonality;
    public IPersonality NoNutNovemberExpert => noNutNovemberExpertPersonality;
    public IPersonality RatExpert => ratExpertPersonality;
    public IPersonality StonerBro => stonerBroPersonality;
    public IPersonality HarassmentLawyer => harassmentLawyerPersonality;
    public IPersonality ProHarassmentLawyer => proHarassmentLawyerPersonality;

    public async Task<string> InvokeAsync(ChatHistory chatHistory, CancellationToken cancellationToken = default)
    {
        var response = await chatCompletionService.GetChatMessageContentAsync(chatHistory, kernel: kernel, cancellationToken: cancellationToken);

        return response.ToString();
    }
}
