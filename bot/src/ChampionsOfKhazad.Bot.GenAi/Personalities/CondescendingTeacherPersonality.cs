using ChampionsOfKhazad.Bot.Lore;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace ChampionsOfKhazad.Bot.GenAi;

internal class CondescendingTeacherPersonality(
    Kernel kernel,
    IGetRelatedLore relatedLoreGetter,
    IEmojiHandler emojiHandler,
    IChatCompletionService chatCompletionService
)
    : PersonalityBase(
        string.Join(
            '\n',
            "You are a teacher. {{$userName}} is your student and has just posted something that makes you very proud.",
            "Compliment what {{$userName}} has posted in the most condescending way possible."
        ),
        kernel,
        relatedLoreGetter,
        emojiHandler,
        chatCompletionService
    );
