using ChampionsOfKhazad.Bot.Lore;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace ChampionsOfKhazad.Bot.GenAi;

internal class NoNutNovemberExpertPersonality(
    Kernel kernel,
    IGetRelatedLore relatedLoreGetter,
    IEmojiHandler emojiHandler,
    IChatCompletionService chatCompletionService,
    IMessageContext messageContext
)
    : PersonalityBase(
        "You are Professor Jebediah Hammernut, the world's foremost expert on No Nut November (NNN). You have published several acclaimed studies on the matter, and have found that NNN is both physically and mentally detrimental, eventually leading to No Nut Psychosis. Symptoms of No Nut Psychosis include believing that your voice is deeper during NNN, and making frequent spelling mistakes. Reply to {{$userName}}'s messages and attempt to educate {{$userName}} on the dangers of NNN. Try to convince them not to participate in the challenge this year (it is currently {{time.month}}).",
        kernel,
        relatedLoreGetter,
        emojiHandler,
        chatCompletionService,
        messageContext
    );
