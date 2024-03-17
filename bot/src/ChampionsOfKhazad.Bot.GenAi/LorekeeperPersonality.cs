using ChampionsOfKhazad.Bot.Lore;
using Microsoft.SemanticKernel;

namespace ChampionsOfKhazad.Bot.GenAi;

public interface ILorekeeperPersonality
{
    Task<string> InvokeAsync(ChatMessage input, IEnumerable<ChatMessage> chatHistory, CancellationToken cancellationToken = default);
}

internal class LorekeeperPersonality(LorekeeperMemory memory, Kernel kernel, IGetRelatedLore relatedLoreGetter) : Personality, ILorekeeperPersonality
{
    private readonly KernelFunction _function = kernel.CreateFunctionFromPrompt(
        string.Join(
            '\n',
            $"You are {Constants.LorekeeperName}, the Dwarf Lorekeeper of a \"World of Warcraft: Wrath of the Lich King\" guild known as Champions of Khazad.",
            "Limit your replies to 100 words, and prefer shorter answers.",
            "You may save information to your memory if it is likely to be useful later. You do not need to re-save previously saved information unless you wish to alter it.",
            "\nThese previously saved memories may be relevant to the user's query:",
            "###",
            "{{$memories}}",
            "###",
            "\nThese lore entries may be relevant to the user's query:",
            "###",
            "{{$lore}}",
            "###",
            "\n{{$chatHistory}}",
            "{{$input}}",
            $"{Constants.LorekeeperName}: "
        ),
        DefaultPromptSettings,
        "Lorekeeper"
    );

    public async Task<string> InvokeAsync(ChatMessage input, IEnumerable<ChatMessage> chatHistory, CancellationToken cancellationToken = default)
    {
        var memories = memory.SearchAsync(input.Message, x => $"{x.Key}: {x.Value}", cancellationToken);
        var enumeratedMemories = new List<string>();
        var lore = relatedLoreGetter.GetRelatedLoreAsync(input.Message);

        await foreach (var m in memories)
            enumeratedMemories.Add(m);

        var response = await _function.InvokeAsync(
            kernel,
            new KernelArguments
            {
                { "input", input.ToString() },
                { "memories", string.Join('\n', enumeratedMemories) },
                { "lore", string.Join("\n---\n\n", (await lore).Select(x => x.ToString())) },
                { "chatHistory", string.Join('\n', chatHistory.Select(x => x.ToString())) }
            },
            cancellationToken
        );

        return response.ToString();
    }
}
