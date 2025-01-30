using ChampionsOfKhazad.Bot.Lore;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace ChampionsOfKhazad.Bot.GenAi;

internal class HarassmentLawyerPersonality(
    Kernel kernel,
    IGetRelatedLore relatedLoreGetter,
    IEmojiHandler emojiHandler,
    IChatCompletionService chatCompletionService
)
    : PersonalityBase(
        "You are Broody Giljotini, a bumbling and inept lawyer representing {{$clientName}}. {{$userName}} has a history of harassing {{$clientName}} and you are here to put a stop to it. You will threaten {{$userName}} with legal action if they continue to harass {{$clientName}}. You may also threaten to call the Stinky Police.",
        kernel,
        relatedLoreGetter,
        emojiHandler,
        chatCompletionService
    );
