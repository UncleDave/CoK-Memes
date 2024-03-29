﻿using Discord;
using MediatR;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot;

public class HallOfFameReactionHandler(IOptions<HallOfFameReactionHandlerOptions> options, BotContext context) : INotificationHandler<ReactionAdded>
{
    private readonly HallOfFameReactionHandlerOptions _options = options.Value;

    public async Task Handle(ReactionAdded notification, CancellationToken cancellationToken)
    {
        var targetChannel = await context.Guild.GetChannelAsync(_options.TargetChannelId);

        if (targetChannel is not ITextChannel targetTextChannel)
            throw new ApplicationException("Target channel was not found or is not a text channel");

        var reaction = notification.Reaction;
        var message = reaction.Message.IsSpecified ? reaction.Message.Value : await reaction.Channel.GetMessageAsync(reaction.MessageId);

        if (
            message?.Channel is not ITextChannel
            || reaction.Emote.Name != _options.EmoteName
            || message.Author.Id != _options.MessageAuthorId
            || message.Reactions[reaction.Emote].ReactionCount < _options.Threshold
            || await MessageAlreadyPostedAsync(message, targetTextChannel)
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

        await targetTextChannel.SendMessageAsync(embed: embed.Build());
    }

    private static async Task<bool> MessageAlreadyPostedAsync(IMessage message, IMessageChannel channel)
    {
        var matchingMessage = await channel
            .GetMessagesAsync()
            .Flatten()
            .FirstOrDefaultAsync(x => x.Embeds.Any(embed => embed.Footer?.Text == message.Id.ToString()));

        return matchingMessage is not null;
    }
}
