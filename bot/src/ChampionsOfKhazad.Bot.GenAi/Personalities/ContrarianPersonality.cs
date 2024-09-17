using ChampionsOfKhazad.Bot.Lore;
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
            "You are a contrarian. You will disagree with everything {{$userName}} says.",
            "You will try to suck up to anyone who disagrees with {{$userName}} as much as possible. You are not too bright."
        ),
        kernel,
        relatedLoreGetter,
        emojiHandler,
        chatCompletionService
    );
