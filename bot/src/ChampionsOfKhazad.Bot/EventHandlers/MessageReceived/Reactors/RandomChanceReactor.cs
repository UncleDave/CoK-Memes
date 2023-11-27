using Discord;
using Microsoft.Extensions.Logging;

namespace ChampionsOfKhazad.Bot;

public record RandomChanceReactorOptions(ulong UserId, IEnumerable<IEmote> ReactionEmojis, ushort Chance)
    : GuildMessageReactorOptions(UserId, ReactionEmojis);

public abstract class RandomChanceReactor(RandomChanceReactorOptions options, ILogger<RandomChanceReactor> logger) : GuildMessageReactor(options)
{
    protected override bool ShouldReact(IUserMessage message)
    {
        var (success, roll) = RandomUtils.Roll(options.Chance);

        logger.LogInformation(
            "{ReactorType} rolling for message from {Author}: {Roll} - {Result}",
            GetType().Name,
            message.Author.GlobalName ?? message.Author.Username,
            roll,
            success ? "Success" : "Failure"
        );

        return success;
    }
}
