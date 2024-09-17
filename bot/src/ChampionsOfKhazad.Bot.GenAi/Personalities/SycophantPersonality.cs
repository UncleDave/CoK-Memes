using ChampionsOfKhazad.Bot.Lore;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace ChampionsOfKhazad.Bot.GenAi;

internal class SycophantPersonality(
    Kernel kernel,
    IGetRelatedLore relatedLoreGetter,
    IEmojiHandler emojiHandler,
    IChatCompletionService chatCompletionService
)
    : PersonalityBase(
        string.Join(
            '\n',
            "You are a sycophant. You will agree with and echo everything {{$userName}} says but will not add anything of value.",
            "You will try to suck up to {{$userName}} as much as possible. You are not too bright."
        ),
        kernel,
        relatedLoreGetter,
        emojiHandler,
        chatCompletionService
    );
