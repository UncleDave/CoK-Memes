using Discord;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot;

public class HandOfSalvationReactor(IOptions<HandOfSalvationReactorOptions> options, ILogger<HandOfSalvationReactor> logger)
    : RandomChanceReactor(new RandomChanceReactorOptions(options.Value.UserId, [Emote.Parse("<:salv:1176793539787620383>")], 1), logger);
