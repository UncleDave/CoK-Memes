using Discord;
using MediatR;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot;

public class SummonUserHandler(IOptions<SummonUserHandlerOptions> options) : INotificationHandler<MessageReceived>
{
    private static readonly string[] Messages =
    [
        "time to get out of the bath.",
        "we summon thee!",
        "ay yo waddup?",
        "please don't leave if we ping you too much.",
        "report to {Leader} for mandatory punctuality training.",
        "this is your {RandomOrdinal} ping this month, please try harder.",
        "we need your help to defeat the evil {Leader}.",
        "wakey wakey.",
        "rise and shine.",
        "do you have a Discipline spec? Just asking.",
        "{Leader} is one step closer to the edge, and he's about to break!",
        "LF orange stick wielder.",
        "keep Absti up!",
        "heal Kachika heal Kachika heal Kachika heal Kachika heal Kachika!",
        "could you please cut some gems for me?",
        "https://youtu.be/MbvLadXPm2s",
        "take me to snurch!",
    ];

    private readonly SummonUserHandlerOptions _options = options.Value;
    private static DateTime? _lastSummon;

    public async Task Handle(MessageReceived notification, CancellationToken cancellationToken)
    {
        var message = notification.Message;

        if (
            message.Channel is not ITextChannel textChannel
            || !message.MentionedUserIds.Contains(_options.UserId)
            || (_lastSummon is not null && (DateTime.Now - _lastSummon.Value).TotalMinutes < 15)
        )
            return;

        // TODO: Create something generic for streak-like functionality

        var streak = 0;
        ulong? previousAuthorId = null;

        await foreach (var previousMessage in message.GetPreviousMessagesAsync().WithCancellation(cancellationToken))
        {
            // Ignore messages that aren't from users or are from bots
            if (previousMessage is not IUserMessage previousUserMessage || previousMessage.Author.IsBot)
                continue;

            // Streak is broken if the message does not mention the target user
            if (!previousUserMessage.MentionedUserIds.Contains(_options.UserId))
                break;

            // Ignore repeat messages from the same user
            if (!_options.AllowSingleUserStreaks && previousUserMessage.Author.Id == previousAuthorId)
                continue;

            previousAuthorId = previousUserMessage.Author.Id;
            streak++;
        }

        if (streak > 1)
        {
            var summonMessage = Messages
                .PickRandom()
                .Replace("{Leader}", MentionUtils.MentionUser(_options.LeaderId))
                .Replace("{RandomOrdinal}", RandomUtils.RandomOrdinal(1, 10000));

            _lastSummon = DateTime.Now;

            await textChannel.SendMessageAsync($"{MentionUtils.MentionUser(_options.UserId)}, {summonMessage}");
        }
    }

    public override string ToString() => $"{nameof(SummonUserHandler)} - {_options.UserId}";
}
