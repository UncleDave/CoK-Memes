﻿using ChampionsOfKhazad.Bot.Lore;
using Microsoft.SemanticKernel;

namespace ChampionsOfKhazad.Bot.GenAi;

internal class LorekeeperPersonality(Kernel kernel, IGetRelatedLore relatedLoreGetter, IEmojiHandler emojiHandler)
    : PersonalityBase,
        IPersonality
{
    private readonly KernelFunction _function = kernel.CreateFunctionFromPrompt(
        string.Join(
            '\n',
            $"You are {Constants.LorekeeperName}, the Dwarf Lorekeeper of a \"World of Warcraft: Wrath of the Lich King\" guild known as Champions of Khazad.",
            "Limit your replies to 100 words, and prefer shorter answers. Do not ask the user if they need anything else after you have answered their query.",
            "The user's query is the last message in the chat history and was directed at you, do not handle previous user prompts that you have already answered.",
            "Do not invoke plugins unless necessary to answer the user's query.",
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

    public async Task<string> InvokeAsync(ChatMessage input, IEnumerable<ChatMessage> chatHistory, CancellationToken cancellationToken = default)
    {
        var lore = await relatedLoreGetter.GetRelatedLoreAsync(input.Message);

        var response = await _function.InvokeAsync(
            kernel,
            new KernelArguments
            {
                { "input", input.ToString() },
                { "lore", string.Join("\n---\n\n", lore.Select(x => x.ToString())) },
                { "chatHistory", string.Join('\n', chatHistory.Select(x => x.ToString())) },
                { "emojis", string.Join(' ', emojiHandler.GetEmojis()) }
            },
            cancellationToken
        );

        return emojiHandler.ProcessMessage(response.ToString());
    }
}
