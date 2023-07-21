using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot;

public class HallOfFameReactionHandler : IReactionAddedEventHandler
{
    private readonly HallOfFameReactionHandlerOptions _options;
    private ITextChannel? _targetChannel;

    public HallOfFameReactionHandler(IOptions<HallOfFameReactionHandlerOptions> options)
    {
        _options = options.Value;
    }

    public async Task StartAsync(BotContext context)
    {
        var targetChannel = await context.Guild.GetChannelAsync(_options.TargetChannelId);

        if (targetChannel is not ITextChannel textChannel)
            throw new InvalidOperationException(
                "Target channel was not found or is not a text channel"
            );

        _targetChannel = textChannel;
    }

    public async Task HandleReactionAsync(SocketReaction reaction)
    {
        if (_targetChannel is null)
            throw new InvalidOperationException(
                $"{nameof(HallOfFameReactionHandler)} has not been started or channel {_options.TargetChannelId} could not be found"
            );

        var message = reaction.Message.IsSpecified
            ? reaction.Message.Value
            : await reaction.Channel.GetMessageAsync(reaction.MessageId);

        if (
            message?.Channel is not ITextChannel
            || reaction.Emote.Name != _options.EmoteName
            || message.Author.Id != _options.MessageAuthorId
            || message.Reactions[reaction.Emote].ReactionCount < _options.Threshold
            || await MessageAlreadyPostedAsync(message)
        )
            return;

        var embed = new EmbedBuilder();

        embed
            .WithAuthor(
                message.Author is IGuildUser { DisplayName: not null } guildUser
                    ? guildUser.DisplayName
                    : message.Author.GlobalName ?? message.Author.Username,
                message.Author.GetAvatarUrl(size: 24) ?? message.Author.GetDefaultAvatarUrl()
            )
            .WithDescription($"[Jump to message]({message.GetJumpUrl()})\n\n{message.Content}")
            .WithColor(Color.DarkGreen)
            .WithFooter(message.Id.ToString())
            .WithTimestamp(message.Timestamp);

        await _targetChannel.SendMessageAsync(embed: embed.Build());
    }

    private async Task<bool> MessageAlreadyPostedAsync(IMessage message)
    {
        var matchingMessage = await _targetChannel!
            .GetMessagesAsync()
            .Flatten()
            .FirstOrDefaultAsync(
                x => x.Embeds.Any(embed => embed.Footer?.Text == message.Id.ToString())
            );

        return matchingMessage is not null;
    }
}
