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

    extension(IUserMessage message)
    {
        public async Task ReplyInChunksAsync(string content, CancellationToken cancellationToken = default)
        {
            var chunks = SplitMessageContent(content);
            var requestOptions = new RequestOptions { CancelToken = cancellationToken };

            await message.ReplyAsync(chunks[0], options: requestOptions);

            foreach (var chunk in chunks.Skip(1))
                await message.Channel.SendMessageAsync(chunk, options: requestOptions);
        }
    }

    extension(IMessageChannel channel)
    {
        public async Task SendMessageInChunksAsync(string content, CancellationToken cancellationToken = default)
        {
            var requestOptions = new RequestOptions { CancelToken = cancellationToken };

            foreach (var chunk in SplitMessageContent(content))
                await channel.SendMessageAsync(chunk, options: requestOptions);
        }
    }

    public static IMessageContext ToMessageContext(this IUserMessage message) => new DiscordMessageContext(message);

    internal static IReadOnlyList<string> SplitMessageContent(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (content.Length <= DiscordConfig.MaxMessageSize)
            return [content];

        List<string> chunks = [];
        var offset = 0;

        while (content.Length - offset > DiscordConfig.MaxMessageSize)
        {
            var chunkLength = FindChunkLength(content, offset);
            chunks.Add(content.Substring(offset, chunkLength));
            offset += chunkLength;
        }

        chunks.Add(content[offset..]);
        return chunks;
    }

    private static int FindChunkLength(string content, int offset)
    {
        var candidate = content.AsSpan(offset, DiscordConfig.MaxMessageSize);
        var splitIndex = candidate.LastIndexOf("\n\n");

        if (splitIndex >= 0)
            return splitIndex + 2;

        splitIndex = candidate.LastIndexOf('\n');

        if (splitIndex >= 0)
            return splitIndex + 1;

        for (var index = candidate.Length - 1; index >= 0; index--)
        {
            if (char.IsWhiteSpace(candidate[index]))
                return index + 1;
        }

        var chunkLength = DiscordConfig.MaxMessageSize;

        if (char.IsHighSurrogate(content[offset + chunkLength - 1]) && char.IsLowSurrogate(content[offset + chunkLength]))
            chunkLength--;

        return chunkLength;
    }

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
