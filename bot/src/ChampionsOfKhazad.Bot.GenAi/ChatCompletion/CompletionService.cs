using Microsoft.Extensions.AI;

namespace ChampionsOfKhazad.Bot.GenAi;

internal class CompletionService(
    IChatClient chatClient,
    LorekeeperPersonality lorekeeperPersonality,
    SycophantPersonality sycophantPersonality,
    ContrarianPersonality contrarianPersonality,
    DisappointedTeacherPersonality disappointedTeacherPersonality,
    CondescendingTeacherPersonality condescendingTeacherPersonality,
    NoNutNovemberExpertPersonality noNutNovemberExpertPersonality,
    RatExpertPersonality ratExpertPersonality,
    StonerBroPersonality stonerBroPersonality
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

    public async Task<string> InvokeAsync(ChatHistory chatHistory, CancellationToken cancellationToken = default)
    {
        var response = await chatClient.GetResponseAsync(chatHistory, cancellationToken: cancellationToken);

        return response.Text;
    }
}
