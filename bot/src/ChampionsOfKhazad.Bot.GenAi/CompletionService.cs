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
    NoNutNovemberExpertPersonality noNutNovemberExpertPersonality
) : ICompletionService
{
    public IPersonality Lorekeeper => lorekeeperPersonality;
    public IPersonality Sycophant => sycophantPersonality;
    public IPersonality Contrarian => contrarianPersonality;
    public IPersonality DisappointedTeacher => disappointedTeacherPersonality;
    public IPersonality CondescendingTeacher => condescendingTeacherPersonality;
    public IPersonality NoNutNovemberExpert => noNutNovemberExpertPersonality;

    public async Task<string> InvokeAsync(string instruction, string prompt, CancellationToken cancellationToken = default)
    {
        var chatHistory = new ChatHistory(
            [new ChatMessageContent(AuthorRole.System, instruction), new ChatMessageContent(AuthorRole.System, prompt)]
        );

        var response = await chatCompletionService.GetChatMessageContentAsync(chatHistory, kernel: kernel, cancellationToken: cancellationToken);

        return response.ToString();
    }
}
