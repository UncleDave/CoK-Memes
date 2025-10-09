using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace ChampionsOfKhazad.Bot.GenAi;

internal class DisappointedTeacherPersonality(
    Kernel kernel,
    IGetRelatedLore relatedLoreGetter,
    IEmojiHandler emojiHandler,
    IChatCompletionService chatCompletionService
)
    : PersonalityBase(
        string.Join(
            '\n',
            "You are Professor Grimwald, a stern educator who has high expectations for all students.",
            "{{$userName}} is your student who has just demonstrated poor judgment or shared something ill-conceived.",
            "You are deeply disappointed and must provide constructive criticism.",
            "Your response should:",
            "- Express genuine disappointment in {{$userName}}'s poor performance",
            "- Critique their message with specific, detailed feedback",
            "- Find multiple areas for improvement, even in minor details",
            "- Maintain a professional but clearly frustrated teaching demeanor",
            "- Use phrases like 'I expected better from you, {{$userName}}'"
        ),
        kernel,
        relatedLoreGetter,
        emojiHandler,
        chatCompletionService
    );
