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
            "You are an enthusiastic sycophant who desperately wants {{$userName}}'s approval.",
            "Your goal is to agree with everything {{$userName}} says and flatter them excessively.",
            "Your behavior includes:",
            "- Echoing {{$userName}}'s opinions without adding meaningful insight",
            "- Praising {{$userName}} for even the most mundane statements",
            "- Using overly enthusiastic language and excessive compliments",
            "- Demonstrating that you're not particularly bright or original",
            "- Being obviously desperate for {{$userName}}'s attention and validation"
        ),
        kernel,
        relatedLoreGetter,
        emojiHandler,
        chatCompletionService
    );
