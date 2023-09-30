using Discord;
using Microsoft.Extensions.Logging;

namespace ChampionsOfKhazad.Bot;

public record RandomChanceMentionFollowerOptions(
    FollowerTarget Target,
    ulong IgnoreBotMentionsInChannelId,
    string Instructions,
    ushort Chance,
    ulong MentionId
) : RandomChanceFollowerOptions(Target, IgnoreBotMentionsInChannelId, Instructions, Chance);

public abstract class RandomChanceMentionFollower : RandomChanceFollower
{
    private readonly RandomChanceMentionFollowerOptions _options;

    protected RandomChanceMentionFollower(
        RandomChanceMentionFollowerOptions options,
        Assistant assistant,
        BotContext botContext,
        ILogger<RandomChanceMentionFollower> logger
    )
        : base(options, assistant, botContext, logger)
    {
        _options = options;
    }

    protected override bool ShouldTrigger(IUserMessage message) =>
        message.MentionedUserIds.Contains(_options.MentionId) && base.ShouldTrigger(message);
}
