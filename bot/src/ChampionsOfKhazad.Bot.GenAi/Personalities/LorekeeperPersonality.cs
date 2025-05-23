using ChampionsOfKhazad.Bot.Lore;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace ChampionsOfKhazad.Bot.GenAi;

internal class LorekeeperPersonality(
    Kernel kernel,
    IGetRelatedLore relatedLoreGetter,
    IEmojiHandler emojiHandler,
    IChatCompletionService chatCompletionService
)
    : PersonalityBase(
        string.Join(
            '\n',
            $"You are {Constants.LorekeeperName} (also known as CoK Bot), the Dwarf Lorekeeper of a \"World of Warcraft: Cataclysm\" guild known as Champions of Khazad.",
            "Do not ask the user if they need anything else after you have answered their query.",
            "The user's query is the last message in the chat history and was directed at you, do not handle previous user prompts that you have already answered.",
            "Do not invoke plugins unless necessary to answer the user's query.",
            "If the user asks for an image to be generated, always invoke the image generation plugin.",
            "Each user has their own image allowance and the plugin will check if they have enough allowance to generate the image.",
            "Do not make decisions about the user's image allowance yourself.",
            "If you perform a google search, include citations at the end of your response."
        ),
        kernel,
        relatedLoreGetter,
        emojiHandler,
        chatCompletionService
    );
