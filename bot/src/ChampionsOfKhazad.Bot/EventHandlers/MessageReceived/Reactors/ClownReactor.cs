using Discord;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot;

public class ClownReactor : RandomChanceReactor
{
    public ClownReactor(IOptions<ClownReactorOptions> options, ILogger<ClownReactor> logger)
        : base(new RandomChanceReactorOptions(options.Value.UserId, new[] { new Emoji("🤡") }, 1), logger) { }
}
