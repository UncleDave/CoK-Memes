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

public abstract class RandomChanceTriggerWordsFollower : RandomChanceFollower
{
    private readonly RandomChanceTriggerWordFollowerOptions _options;

    protected RandomChanceTriggerWordsFollower(
        RandomChanceTriggerWordFollowerOptions options,
        Assistant assistant,
        BotContext botContext,
        ILogger<RandomChanceTriggerWordsFollower> logger
    )
        : base(options, assistant, botContext, logger)
    {
        _options = options;
    }

    protected override bool ShouldTrigger(IUserMessage message) =>
        _options.TriggerWords.Any(x => message.CleanContent.ToLowerInvariant().Contains(x)) && base.ShouldTrigger(message);
}
