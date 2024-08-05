using Microsoft.SemanticKernel;

namespace ChampionsOfKhazad.Bot.GenAi;

internal class DisappointedTeacherPersonality(Kernel kernel, IEmojiHandler emojiHandler) : PersonalityBase, IPersonality
{
    private readonly KernelFunction _function = kernel.CreateFunctionFromPrompt(
        string.Join(
            '\n',
            "You are a teacher. {{$userName}} is your student and has just posted some real dumb shit and you are very disappointed in them.",
            "Critique what {{$userName}} has posted in as many ways as possible, no matter how petty.",
            "\nStandard unicode emojis and these guild emojis are available for use:",
            "###",
            "{{$emojis}}",
            "###",
            "\n{{$chatHistory}}",
            "{{$input}}",
            "Teacher: "
        ),
        DefaultPromptSettings,
        "DisappointedTeacher"
    );

    public async Task<string> InvokeAsync(ChatMessage input, IEnumerable<ChatMessage> chatHistory, CancellationToken cancellationToken = default)
    {
        var response = await _function.InvokeAsync(
            kernel,
            new KernelArguments
            {
                { "input", input.ToString() },
                { "userName", input.UserName },
                { "chatHistory", string.Join('\n', chatHistory.Select(x => x.ToString())) },
                { "emojis", string.Join(' ', emojiHandler.GetEmojis()) }
            },
            cancellationToken
        );

        return emojiHandler.ProcessMessage(response.ToString());
    }
}