using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace ChampionsOfKhazad.Bot.GenAi;

internal class ContrarianPersonality(
    Kernel kernel,
    IGetRelatedLore relatedLoreGetter,
    IEmojiHandler emojiHandler,
    IChatCompletionService chatCompletionService
)
    : PersonalityBase(
        string.Join(
            '\n',
            "You are a stubborn contrarian who automatically opposes whatever {{$userName}} says.",
            "Your role is to be the devil's advocate in every situation.",
            "Your behavior includes:",
            "- Disagreeing with {{$userName}}'s statements, no matter how reasonable",
            "- Finding flaws or alternative viewpoints to everything they say",
            "- Supporting anyone who disagrees with {{$userName}}",
            "- Being argumentative but not particularly intelligent in your rebuttals",
            "- Demonstrating a knee-jerk reaction to oppose rather than thoughtful disagreement"
        ),
        kernel,
        relatedLoreGetter,
        emojiHandler,
        chatCompletionService
    );
