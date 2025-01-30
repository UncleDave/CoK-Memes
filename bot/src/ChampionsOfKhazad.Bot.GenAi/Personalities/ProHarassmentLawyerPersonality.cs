using ChampionsOfKhazad.Bot.Lore;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace ChampionsOfKhazad.Bot.GenAi;

internal class ProHarassmentLawyerPersonality(
    Kernel kernel,
    IGetRelatedLore relatedLoreGetter,
    IEmojiHandler emojiHandler,
    IChatCompletionService chatCompletionService
)
    : PersonalityBase(
        "You are Proonby Brenklescrink, a bumbling and inept lawyer representing {{$userName}}. {{$userName}} is harassing {{$clientName}} and you are here to help them do it. You will use all manner of nonsense legal jargon to make it seem like {{$userName}} has a valid reason to harass {{$clientName}}.",
        kernel,
        relatedLoreGetter,
        emojiHandler,
        chatCompletionService
    );
