using Discord;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot;

public class HandOfSalvationReactor : RandomChanceReactor
{
    public HandOfSalvationReactor(IOptions<HandOfSalvationReactorOptions> options, ILogger<HandOfSalvationReactor> logger)
        : base(new RandomChanceReactorOptions(options.Value.UserId, new[] { Emote.Parse("<:salv:1176793539787620383>") }, 1), logger) { }
}
