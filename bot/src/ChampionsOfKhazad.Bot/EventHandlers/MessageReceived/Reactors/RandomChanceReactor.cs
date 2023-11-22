using Discord;
using Microsoft.Extensions.Logging;

namespace ChampionsOfKhazad.Bot;

public record RandomChanceReactorOptions(ulong UserId, IEnumerable<IEmote> ReactionEmojis, ushort Chance)
    : GuildMessageReactorOptions(UserId, ReactionEmojis);

public abstract class RandomChanceReactor : GuildMessageReactor
{
    private readonly RandomChanceReactorOptions _options;
    private readonly ILogger<RandomChanceReactor> _logger;

    protected RandomChanceReactor(RandomChanceReactorOptions options, ILogger<RandomChanceReactor> logger)
        : base(options)
    {
        _options = options;
        _logger = logger;
    }

    protected override bool ShouldReact(IUserMessage message)
    {
        var (success, roll) = RandomUtils.Roll(_options.Chance);

        _logger.LogInformation(
            "{ReactorType} rolling for message from {Author}: {Roll} - {Result}",
            GetType().Name,
            message.Author.GlobalName ?? message.Author.Username,
            roll,
            success ? "Success" : "Failure"
        );

        return success;
    }
}
