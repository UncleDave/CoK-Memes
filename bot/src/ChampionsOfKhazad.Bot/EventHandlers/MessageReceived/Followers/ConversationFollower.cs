using System.ComponentModel.DataAnnotations;
using ChampionsOfKhazad.Bot.GenAi;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot;

public record ConversationFollowerOptions
{
    public const string Key = "Conversation";

    [Range(0.0, 100.0)]
    public double Chance { get; init; }

    public ulong? TargetRoleId { get; init; }

    [Range(0.0, 100.0)]
    public double TargetRoleChance { get; init; } = 1;
}

public class ConversationFollower(
    IOptions<AllFollowersOptions> allFollowersOptions,
    IOptions<ConversationFollowerOptions> options,
    ICompletionService completionService,
    BotContext botContext,
    ILogger<RandomChanceFollowerTriggerStrategy> triggerStrategyLogger
)
    : StrategyFollower(
        allFollowersOptions.Value.IgnoreBotMentionsInChannelId,
        options.Value.TargetRoleId is { } targetRoleId
            ? new ConditionalFollowerTriggerStrategy(
                new TargetRoleFollowerTriggerStrategy(targetRoleId),
                new RandomChanceFollowerTriggerStrategy(options.Value.TargetRoleChance, triggerStrategyLogger),
                new RandomChanceFollowerTriggerStrategy(options.Value.Chance, triggerStrategyLogger)
            )
            : new RandomChanceFollowerTriggerStrategy(options.Value.Chance, triggerStrategyLogger),
        new SplitPersonalityFollowerResponseStrategy(
            [
                completionService.Sycophant,
                completionService.Contrarian,
                completionService.DisappointedTeacher,
                completionService.CondescendingTeacher,
                completionService.StonerBro,
            ],
            botContext.BotId,
            string.Join(
                '\n',
                "You are spontaneously joining an ongoing Discord conversation; the author has not necessarily addressed you.",
                "Respond only to the final user message from the current author. Earlier messages are background, not pending requests.",
                "Previous bot replies may use different personalities. Use only the personality selected for this invocation.",
                "Make one brief, in-character interjection grounded in the latest message; do not invent claims, events, or unseen image details."
            )
        ),
        botContext
    );
