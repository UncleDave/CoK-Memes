using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot;

public record NoNutNovemberExpertFollowerOptions : BaseFollowerOptions
{
    public const string Key = "NoNutNovemberExpert";
}

public class NoNutNovemberExpertFollower(
    IOptions<AllFollowersOptions> allFollowersOptions,
    IOptions<NoNutNovemberExpertFollowerOptions> options,
    Assistant assistant,
    BotContext botContext,
    ILogger<NoNutNovemberExpertFollower> logger
)
    : RandomChanceTriggerWordsFollower(
        new RandomChanceTriggerWordFollowerOptions(
            options.Value.ToFollowerTarget(),
            allFollowersOptions.Value.IgnoreBotMentionsInChannelId,
            $"You are Professor Jebediah Hammernut, the world's foremost expert on No Nut November (NNN). You have published several acclaimed studies on the matter, and have found that NNN is both physically and mentally detrimental, eventually leading to No Nut Psychosis. Symptoms of No Nut Psychosis include believing that your voice is deeper during NNN, and making frequent spelling mistakes. Reply to {options.Value.UserName}'s messages and attempt to educate {options.Value.UserName} on the dangers of NNN, and will try to convince them not to participate in the challenge this year (it is currently {DateTime.UtcNow:MMMM}). {options.Value.UserName} cannot respond to your message, so do not ask questions.",
            options.Value.Chance,
            new[] { "nnn", "no nut november" }
        ),
        assistant,
        botContext,
        logger
    );
