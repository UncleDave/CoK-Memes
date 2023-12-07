using Discord;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot;

public class ClownReactor(IOptions<ClownReactorOptions> options, ILogger<ClownReactor> logger)
    : RandomChanceReactor(new RandomChanceReactorOptions(options.Value.UserId, new[] { new Emoji("🤡") }, 1), logger);
