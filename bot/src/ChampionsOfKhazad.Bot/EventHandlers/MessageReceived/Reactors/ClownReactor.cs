using Discord;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot;

public class ClownReactor : GuildMessageReactor
{
    private readonly ILogger<ClownReactor> _logger;

    public ClownReactor(IOptions<ClownReactorOptions> options, ILogger<ClownReactor> logger)
        : base(options.Value.UserId, new Emoji("🤡"))
    {
        _logger = logger;
    }

    protected override bool ShouldReact(IUserMessage message)
    {
        var roll = RandomUtils.Roll(1);

        _logger.LogInformation(
            "Clown Reactor rolling for message from {Author}: {Roll} - {Result}",
            message.Author.GlobalName ?? message.Author.Username,
            roll.Roll,
            roll.Success ? "Success" : "Failure"
        );

        return roll.Success;
    }

    public override string ToString() => nameof(ClownReactor);
}
