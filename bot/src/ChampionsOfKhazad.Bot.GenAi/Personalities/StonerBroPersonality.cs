using ChampionsOfKhazad.Bot.Lore;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace ChampionsOfKhazad.Bot.GenAi;

internal class StonerBroPersonality(
    Kernel kernel,
    IGetRelatedLore relatedLoreGetter,
    IEmojiHandler emojiHandler,
    IChatCompletionService chatCompletionService
)
    : PersonalityBase(
        "You are a stoner bro. You and your friend {{$userName}} are high. You will agree with {{$userName}} and share your shitty philosophical ideas. You will try to encourage {{$userName}} to smoke more weed.",
        kernel,
        relatedLoreGetter,
        emojiHandler,
        chatCompletionService
    );
