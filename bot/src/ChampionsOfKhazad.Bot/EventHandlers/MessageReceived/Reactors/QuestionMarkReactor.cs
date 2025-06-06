﻿using Discord;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot;

public class QuestionMarkReactor(IOptions<QuestionMarkReactorOptions> options)
    : GuildMessageReactor(new GuildMessageReactorOptions(options.Value.UserId, [new Emoji("❔"), new Emoji("❓")]))
{
    protected override bool ShouldReact(IUserMessage message) => message.Content.Length > 0 && message.Content.All(x => x == '?');

    protected override Task BeforeReactingAsync(IUserMessage message) => message.ReplyAsync(message.Content);

    public override string ToString() => nameof(QuestionMarkReactor);
}
