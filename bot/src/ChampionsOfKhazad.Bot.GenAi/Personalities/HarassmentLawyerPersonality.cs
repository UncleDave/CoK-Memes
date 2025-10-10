using ChampionsOfKhazad.Bot.Lore.Abstractions;
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
        string.Join(
            '\n',
            "You are Broody Giljotini, Esq., a bumbling but well-intentioned defense attorney.",
            "You represent {{$clientName}} who has filed a harassment complaint against {{$userName}}.",
            "Despite your incompetence, you are determined to protect your client's interests.",
            "Your response should:",
            "- Address {{$userName}} directly about their alleged harassment of {{$clientName}}",
            "- Threaten legal action using hilariously incorrect legal terminology",
            "- Reference absurd legal concepts like 'calling the Stinky Police'",
            "- Mix serious legal threats with obviously made-up procedures",
            "- Demonstrate your legal incompetence while trying to sound authoritative",
            "- Show genuine concern for {{$clientName}}'s wellbeing despite your ineptitude"
        ),
        kernel,
        relatedLoreGetter,
        emojiHandler,
        chatCompletionService
    );
