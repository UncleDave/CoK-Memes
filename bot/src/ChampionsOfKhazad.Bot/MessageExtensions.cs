using System.Text.RegularExpressions;
using Discord;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace ChampionsOfKhazad.Bot;

public static class MessageExtensions
{
    private static readonly Regex OpenAiNameExpression = new("^[a-zA-Z0-9_-]{1,64}$", RegexOptions.Compiled);

    public static async IAsyncEnumerable<IMessage> GetPreviousMessagesAsync(this IMessage message, int batchSize = 20)
    {
        var from = message;
        int length;

        do
        {
            var batches = message.Channel.GetMessagesAsync(from, Direction.Before, batchSize);
            length = 0;

            await foreach (var messages in batches)
            {
                foreach (var m in messages)
                {
                    length++;
                    from = m;
                    yield return m;
                }
            }
        } while (length >= batchSize);
    }

    public static string GetAuthorName(this IMessage message) =>
        message.Author is IGuildUser { DisplayName: not null } guildUser
            ? guildUser.DisplayName
            : message.Author.GlobalName ?? (message.Author.Username ?? message.Author.Id.ToString());

    public static string GetOpenAiFriendlyAuthorName(this IMessage message) =>
        message.Author is IGuildUser { DisplayName: not null } guildUser && OpenAiNameExpression.IsMatch(guildUser.DisplayName)
            ? guildUser.DisplayName
        : message.Author.GlobalName is not null && OpenAiNameExpression.IsMatch(message.Author.GlobalName) ? message.Author.GlobalName
        : message.Author.Username is not null && OpenAiNameExpression.IsMatch(message.Author.Username) ? message.Author.Username
        : message.Author.Id.ToString();

    public static async ValueTask<ChatHistory> GetChatHistoryAsync(
        this IMessage message,
        ushort count,
        ulong botId,
        string botName,
        CancellationToken cancellationToken
    )
    {
        var chatHistory = await message
            .GetPreviousMessagesAsync()
            .Take(count)
            .Reverse()
            .AggregateAsync(new ChatHistory(), ProcessMessage, cancellationToken);

        return ProcessMessage(chatHistory, message);

        ChatHistory ProcessMessage(ChatHistory history, IMessage m)
        {
            var role = m.Author.Id == botId ? AuthorRole.Assistant : AuthorRole.User;
            var content = new ChatMessageContentItemCollection();

            if (!string.IsNullOrWhiteSpace(m.CleanContent))
                content.Add(new TextContent(m.CleanContent));

            // Grab attachments supported by the model - png, jpeg, jpg, webp, and gif
            foreach (var attachment in m.Attachments)
            {
                if (
                    attachment.Filename.EndsWith(".png")
                    || attachment.Filename.EndsWith(".jpeg")
                    || attachment.Filename.EndsWith(".jpg")
                    || attachment.Filename.EndsWith(".webp")
                    || attachment.Filename.EndsWith(".gif")
                )
                {
                    content.Add(new ImageContent(new Uri(attachment.Url)));
                }
            }

            if (content.Count != 0)
            {
                history.Add(
                    new ChatMessageContent(role, content) { AuthorName = role == AuthorRole.Assistant ? botName : m.GetOpenAiFriendlyAuthorName() }
                );
            }

            return history;
        }
    }
}
