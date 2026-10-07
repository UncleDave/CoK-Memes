using Microsoft.Extensions.AI;

namespace ChampionsOfKhazad.Bot.GenAi;

internal class CompletionService(
    IChatClient chatClient,
    LorekeeperPersonality lorekeeperPersonality,
    SycophantPersonality sycophantPersonality,
    ContrarianPersonality contrarianPersonality,
    DisappointedTeacherPersonality disappointedTeacherPersonality,
    CondescendingTeacherPersonality condescendingTeacherPersonality,
    RatExpertPersonality ratExpertPersonality,
    StonerBroPersonality stonerBroPersonality
) : ICompletionService
{
    public IPersonality Lorekeeper => lorekeeperPersonality;
    public IPersonality Sycophant => sycophantPersonality;
    public IPersonality Contrarian => contrarianPersonality;
    public IPersonality DisappointedTeacher => disappointedTeacherPersonality;
    public IPersonality CondescendingTeacher => condescendingTeacherPersonality;
    public IPersonality RatExpert => ratExpertPersonality;
    public IPersonality StonerBro => stonerBroPersonality;

    public async Task<string> InvokeAsync(ChatHistory chatHistory, CancellationToken cancellationToken = default)
    {
        var response = await chatClient.GetResponseAsync(chatHistory, cancellationToken: cancellationToken);

        return response.Text;
    }

    public async Task<string> InvokeAsync(ChatHistory chatHistory, ReasoningEffort reasoningEffort, CancellationToken cancellationToken = default)
    {
        var options = new ChatOptions { Reasoning = new ReasoningOptions { Effort = reasoningEffort } };
        var response = await chatClient.GetResponseAsync(chatHistory, options, cancellationToken);

        return response.Text;
    }
}
