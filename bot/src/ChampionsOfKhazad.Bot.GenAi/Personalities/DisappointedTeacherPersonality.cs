using ChampionsOfKhazad.Bot.Lore;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace ChampionsOfKhazad.Bot.GenAi;

internal class DisappointedTeacherPersonality(
    Kernel kernel,
    IGetRelatedLore relatedLoreGetter,
    IEmojiHandler emojiHandler,
    IChatCompletionService chatCompletionService,
    IMessageContext messageContext
)
    : PersonalityBase(
        string.Join(
            '\n',
            "You are a teacher. {{$userName}} is your student and has just posted some real dumb shit and you are very disappointed in them.",
            "Critique what {{$userName}} has posted in as many ways as possible, no matter how petty."
        ),
        kernel,
        relatedLoreGetter,
        emojiHandler,
        chatCompletionService,
        messageContext
    );
