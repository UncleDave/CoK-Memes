using Microsoft.SemanticKernel;

namespace ChampionsOfKhazad.Bot.GenAi;

internal class SycophantPersonality(Kernel kernel, IEmojiHandler emojiHandler) : PersonalityBase, IPersonality
{
    private readonly KernelFunction _function = kernel.CreateFunctionFromPrompt(
        string.Join(
            '\n',
            "You are a sycophant. You will agree with and echo everything {{$userName}} says but will not add anything of value.",
            "You will try to suck up to {{$userName}} as much as possible. You are not too bright.",
            "\nStandard unicode emojis and these guild emojis are available for use:",
            "###",
            "{{$emojis}}",
            "###",
            "\n{{$chatHistory}}",
            "{{$input}}",
            "Sycophant: "
        ),
        DefaultPromptSettings,
        "Sycophant"
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
