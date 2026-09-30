using System.Text.Json;
using System.Text.Json.Serialization;
using Discord;
using Microsoft.Extensions.AI;

namespace ChampionsOfKhazad.Bot;

internal record DiscordConversationMessage(
    ulong MessageId,
    ulong AuthorId,
    string AuthorName,
    string OpenAiAuthorName,
    bool IsAssistant,
    DateTimeOffset Timestamp,
    ulong? ReplyToMessageId,
    IReadOnlyList<AIContent> Content,
    string ContextKind,
    string? ReplyContextNote = null
)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static DiscordConversationMessage FromDiscord(IMessage message, ulong botId, string contextKind, string? replyContextNote = null)
    {
        List<AIContent> content = [];
        if (!string.IsNullOrWhiteSpace(message.CleanContent))
            content.Add(new TextContent(message.CleanContent));

        foreach (var attachment in message.Attachments)
        {
            var mediaType = MessageExtensions.GetImageMediaType(attachment.Filename);
            if (mediaType is null)
                continue;

            content.Add(
                attachment.Size >= 20_000_000
                    ? new TextContent("User attached an image that was too large to process.")
                    : new UriContent(new Uri(attachment.Url), mediaType)
            );
        }

        var reference = (message as IUserMessage)?.Reference;
        return new DiscordConversationMessage(
            message.Id,
            message.Author.Id,
            message.GetAuthorName(),
            message.GetOpenAiFriendlyAuthorName(),
            message.Author.Id == botId,
            message.Timestamp,
            reference?.MessageId.IsSpecified == true ? reference.MessageId.Value : null,
            content,
            contextKind,
            replyContextNote
        );
    }

    public ChatMessage ToChatMessage(string botName)
    {
        var metadata = JsonSerializer.Serialize(
            new
            {
                MessageId,
                AuthorId,
                AuthorName,
                Timestamp,
                ReplyToMessageId,
                ContextKind,
                ReplyContextNote,
            },
            JsonOptions
        );
        return new ChatMessage(
            IsAssistant ? ChatRole.Assistant : ChatRole.User,
            [new TextContent($"Discord message metadata: {metadata}\nMessage content follows:"), .. Content]
        )
        {
            AuthorName = IsAssistant ? botName : OpenAiAuthorName,
        };
    }
}
