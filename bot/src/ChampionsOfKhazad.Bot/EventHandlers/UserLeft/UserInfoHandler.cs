using Discord;
using Discord.WebSocket;
using MediatR;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot;

public class UserInfoHandler(IOptions<UserInfoHandlerOptions> options, BotContext context) : INotificationHandler<UserLeft>
{
    private readonly UserInfoHandlerOptions _options = options.Value;

    public async Task Handle(UserLeft notification, CancellationToken cancellationToken)
    {
        var targetChannel = await context.Guild.GetChannelAsync(_options.TargetChannelId);

        if (targetChannel is not ITextChannel textChannel)
            throw new ApplicationException("Target channel was not found or is not a text channel");

        var user = notification.User;
        var guildUser = user as SocketGuildUser ?? await context.Guild.GetUserAsync(user.Id);
        var displayName = guildUser?.DisplayName ?? user.GlobalName ?? user.Username;
        var now = DateTimeOffset.UtcNow;

        var embed = new EmbedBuilder()
            .WithTitle("User left the guild 👋")
            .WithAuthor(displayName, user.GetAvatarUrl(size: 64) ?? user.GetDefaultAvatarUrl())
            .AddField("User", $"{displayName} ({user.Id})")
            .WithColor(Color.DarkGrey)
            .WithTimestamp(now);

        await textChannel.SendMessageAsync(embed: embed.Build());
    }

    public override string ToString() => nameof(UserInfoHandler);
}
