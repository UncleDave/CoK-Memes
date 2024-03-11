using System.Text.Json;
using Discord;
using MediatR;

namespace ChampionsOfKhazad.Bot;

public class SummariseSlashCommand(Assistant assistant, BotContext botContext) : INotificationHandler<SummariseSlashCommandExecuted>
{
    private const string FooterText = "Summarised for";
    private static readonly JsonSerializerOptions JsonSerializerOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task Handle(SummariseSlashCommandExecuted notification, CancellationToken cancellationToken)
    {
        await notification.Command.DeferAsync();

        var messages = (await notification.Command.Channel.GetMessagesAsync(50).FlattenAsync()).ToList();

        if (messages.Count < 10)
        {
            await notification.Command.FollowupAsync("There aren't enough messages in this channel/thread to summarise.", ephemeral: true);
            return;
        }

        var messagesSinceLastSummary = messages
            .TakeWhile(x =>
                !(x.Author.Id == botContext.Client.CurrentUser.Id && (x.Embeds.SingleOrDefault()?.Footer?.Text.StartsWith(FooterText) ?? false))
            )
            .Reverse()
            .ToList();

        if (messagesSinceLastSummary.Count < 10)
        {
            await notification.Command.FollowupAsync("There haven't been enough messages since my last summary.", ephemeral: true);
            return;
        }

        var formattedMessages = string.Join(
            '\n',
            messagesSinceLastSummary.Select(x => $"{(x.Author as IGuildUser)?.DisplayName ?? x.Author.GlobalName ?? x.Author.Username}: {x.Content}")
        );

        var summary = await assistant.RespondAsync(
            $"Summarise the following messages from the #{notification.Command.Channel.Name} channel. Respond with a JSON object in the following format: {{ \"title\": \"Short title that summarises the messages\", \"description\": \"The full summary of the messages\" }}",
            formattedMessages
        );

        var parsedSummary = JsonSerializer.Deserialize<Summary>(summary.Trim(['`', 'j', 's', 'o', 'n', '\n']), JsonSerializerOptions);

        if (parsedSummary is null)
        {
            await notification.Command.FollowupAsync(
                "Something went wrong while summarising that for you, sorry! It's possible I'm having a stroke.",
                ephemeral: true
            );
            return;
        }

        var embed = new EmbedBuilder()
            .WithTitle(parsedSummary.Title)
            .WithDescription(parsedSummary.Description)
            .WithFooter(
                $"{FooterText} {(notification.Command.User as IGuildUser)?.DisplayName ?? notification.Command.User.GlobalName ?? notification.Command.User.Username}",
                notification.Command.User.GetAvatarUrl()
            )
            .WithCurrentTimestamp()
            .Build();

        await notification.Command.FollowupAsync(embeds: [embed]);
    }
}
