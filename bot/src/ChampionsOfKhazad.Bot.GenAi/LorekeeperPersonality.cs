using ChampionsOfKhazad.Bot.Lore;
using Microsoft.SemanticKernel;

namespace ChampionsOfKhazad.Bot.GenAi;

public interface ILorekeeperPersonality
{
    Task<string> InvokeAsync(
        ChatMessage input,
        IEnumerable<ChatMessage> chatHistory,
        IEnumerable<string> emojis,
        CancellationToken cancellationToken = default
    );
}

internal class LorekeeperPersonality(LorekeeperMemory memory, Kernel kernel, IGetRelatedLore relatedLoreGetter) : Personality, ILorekeeperPersonality
{
    private readonly KernelFunction _function = kernel.CreateFunctionFromPrompt(
        string.Join(
            '\n',
            $"You are {Constants.LorekeeperName}, the Dwarf Lorekeeper of a \"World of Warcraft: Wrath of the Lich King\" guild known as Champions of Khazad.",
            "Limit your replies to 100 words, and prefer shorter answers.",
            "You may save information from the user's message to your memory if it is likely to be useful later. You do not need to re-save previously saved information unless you wish to alter it.",
            "\nThese previously saved memories may be relevant to the user's query:",
            "###",
            "{{$memories}}",
            "###",
            "\nThese lore entries may be relevant to the user's query:",
            "###",
            "{{$lore}}",
            "###",
            "\nStandard unicode emojis and these guild emojis are available for use:",
            "###",
            "{{$emojis}}",
            "###",
            "\n{{$chatHistory}}",
            "{{$input}}",
            $"{Constants.LorekeeperName}: "
        ),
        DefaultPromptSettings,
        "Lorekeeper"
    );

    public async Task<string> InvokeAsync(
        ChatMessage input,
        IEnumerable<ChatMessage> chatHistory,
        IEnumerable<string> emojis,
        CancellationToken cancellationToken = default
    )
    {
        var memories = GetRelatedMemoriesAsync(input.Message, cancellationToken);
        var lore = relatedLoreGetter.GetRelatedLoreAsync(input.Message);

        var response = await _function.InvokeAsync(
            kernel,
            new KernelArguments
            {
                { "input", input.ToString() },
                { "memories", string.Join('\n', await memories) },
                { "lore", string.Join("\n---\n\n", (await lore).Select(x => x.ToString())) },
                { "chatHistory", string.Join('\n', chatHistory.Select(x => x.ToString())) },
                { "emojis", string.Join(' ', emojis.Select(x => $":{x}:")) }
            },
            cancellationToken
        );

        return response.ToString();
    }

    private async Task<IReadOnlyList<string>> GetRelatedMemoriesAsync(string input, CancellationToken cancellationToken)
    {
        var memories = memory.SearchAsync(input, x => $"{x.Key}: {x.Value}", cancellationToken);
        var enumeratedMemories = new List<string>();

        await foreach (var m in memories)
            enumeratedMemories.Add(m);

        return enumeratedMemories;
    }
}
