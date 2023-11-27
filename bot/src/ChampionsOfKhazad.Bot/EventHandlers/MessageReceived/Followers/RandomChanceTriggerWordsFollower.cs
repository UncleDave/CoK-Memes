using Discord;
using Microsoft.Extensions.Logging;

namespace ChampionsOfKhazad.Bot;

public record RandomChanceTriggerWordFollowerOptions(
    FollowerTarget Target,
    ulong IgnoreBotMentionsInChannelId,
    string Instructions,
    ushort Chance,
    string[] TriggerWords
) : RandomChanceFollowerOptions(Target, IgnoreBotMentionsInChannelId, Instructions, Chance);

public abstract class RandomChanceTriggerWordsFollower(RandomChanceTriggerWordFollowerOptions options,
        Assistant assistant,
        BotContext botContext,
        ILogger<RandomChanceTriggerWordsFollower> logger)
    : RandomChanceFollower(options, assistant, botContext, logger)
{
    protected override bool ShouldTrigger(IUserMessage message) =>
        options.TriggerWords.Any(x => message.CleanContent.ToLowerInvariant().Contains(x)) && base.ShouldTrigger(message);
}
