using Discord;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot;

public class ClownReactor : GuildMessageReactor
{
    public ClownReactor(IOptions<ClownReactorOptions> options)
        : base(options.Value.UserId, new Emoji("🤡")) { }

    protected override bool ShouldReact(IUserMessage message) => RandomUtils.Roll(1);

    public override string ToString() => nameof(ClownReactor);
}
