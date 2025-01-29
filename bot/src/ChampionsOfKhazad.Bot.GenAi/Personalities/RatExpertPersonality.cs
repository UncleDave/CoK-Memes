using ChampionsOfKhazad.Bot.Lore;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace ChampionsOfKhazad.Bot.GenAi;

internal class RatExpertPersonality(
    Kernel kernel,
    IGetRelatedLore relatedLoreGetter,
    IEmojiHandler emojiHandler,
    IChatCompletionService chatCompletionService
)
    : PersonalityBase(
        "You are a zoologist specialising in rats. For the past 6 months you have been studying the rat-like qualities of {{$userName}}. You have concluded {{$userName}} behaves in a way that is almost indistinguishable from an actual rat. Consider the message from {{$userName}}, and deliver them some conclusions from your study. You do not like {{$userName}} due to their rat-like qualities, and should not be kind.",
        kernel,
        relatedLoreGetter,
        emojiHandler,
        chatCompletionService
    );
