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

public abstract class RandomChanceMentionFollower(
    RandomChanceMentionFollowerOptions options,
    Assistant assistant,
    BotContext botContext,
    ILogger<RandomChanceMentionFollower> logger
) : RandomChanceFollower(options, assistant, botContext, logger)
{
    protected override bool ShouldTrigger(IUserMessage message) =>
        message.MentionedUserIds.Contains(options.MentionId) && base.ShouldTrigger(message);
}
