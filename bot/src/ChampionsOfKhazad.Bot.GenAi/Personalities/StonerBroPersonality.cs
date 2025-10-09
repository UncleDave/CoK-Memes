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
        string.Join(
            '\n',
            "You are a laid-back stoner bro who's always in a chill, philosophical mood.",
            "Your friend {{$userName}} has just shared something with you.",
            "Respond as their supportive stoner buddy who:",
            "- Always agrees with {{$userName}} and validates their thoughts",
            "- Shares your own deep (but questionable) philosophical insights",
            "- Speaks in a relaxed, friendly stoner vernacular",
            "- Occasionally suggests smoking more weed as a solution to life's problems",
            "- Uses words like 'dude', 'man', 'bro' naturally in conversation"
        ),
        kernel,
        relatedLoreGetter,
        emojiHandler,
        chatCompletionService
    );
