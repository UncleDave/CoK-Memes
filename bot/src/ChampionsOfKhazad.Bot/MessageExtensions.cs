using System.Text.RegularExpressions;
using ChampionsOfKhazad.Bot.GenAi;
using Discord;
using Microsoft.Extensions.AI;

namespace ChampionsOfKhazad.Bot;

public static class MessageExtensions
{
    private static readonly Regex OpenAiNameExpression = new("^[a-zA-Z0-9_-]{1,64}$", RegexOptions.Compiled);

    extension(IMessage message)
    {
        public async IAsyncEnumerable<IMessage> GetPreviousMessagesAsync(ushort batchSize = 20)
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

        public string GetAuthorName() => message.Author.GetName();

        public string GetOpenAiFriendlyAuthorName() =>
            message.Author is IGuildUser { DisplayName: not null } guildUser && OpenAiNameExpression.IsMatch(guildUser.DisplayName)
                ? guildUser.DisplayName
            : message.Author.GlobalName is not null && OpenAiNameExpression.IsMatch(message.Author.GlobalName) ? message.Author.GlobalName
            : message.Author.Username is not null && OpenAiNameExpression.IsMatch(message.Author.Username) ? message.Author.Username
            : message.Author.Id.ToString();

        public async ValueTask<ChatHistory> GetChatHistoryAsync(ushort count, ulong botId, string botName, CancellationToken cancellationToken)
        {
            var chatHistory = await message
                .GetPreviousMessagesAsync()
                .Where(x => x.Author.Id != botId || !x.CleanContent.StartsWith(GenAi.Constants.ImageGenerationConfirmationMessage))
                .Take(count)
                .Reverse()
                .AggregateAsync(new ChatHistory(), ProcessMessage, cancellationToken);

            return ProcessMessage(chatHistory, message);

            ChatHistory ProcessMessage(ChatHistory history, IMessage m)
            {
                var role = m.Author.Id == botId ? ChatRole.Assistant : ChatRole.User;
                List<AIContent> content = [];

                if (!string.IsNullOrWhiteSpace(m.CleanContent))
                    content.Add(new TextContent(m.CleanContent));

                foreach (var attachment in m.Attachments)
                {
                    var mediaType = GetImageMediaType(attachment.Filename);

                    if (mediaType is null)
                        continue;

                    if (attachment.Size >= 20_000_000)
                    {
                        content.Add(new TextContent("User attached an image that was too large to process."));
                    }
                    else
                    {
                        content.Add(new UriContent(new Uri(attachment.Url), mediaType));
                    }
                }

                if (content.Count != 0)
                {
                    history.Add(
                        new ChatMessage(role, content) { AuthorName = role == ChatRole.Assistant ? botName : m.GetOpenAiFriendlyAuthorName() }
                    );
                }

                return history;
            }
        }
    }

    public static IMessageContext ToMessageContext(this IUserMessage message) => new DiscordMessageContext(message);

    private static string? GetImageMediaType(string filename) =>
        Path.GetExtension(filename).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpeg" or ".jpg" => "image/jpeg",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => null,
        };
}
