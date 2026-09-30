using System.Reflection;
using Discord;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ChampionsOfKhazad.Bot.Tests;

internal class DiscordConversationFixture
{
    public const ulong BotId = 9;
    public const ulong AdminId = 1;
    public const ulong ChannelId = 100;
    public Dictionary<ulong, IUserMessage> Messages { get; } = [];
    public List<ulong> FetchedIds { get; } = [];
    public List<string> Reactions { get; } = [];
    public List<string> Replies { get; } = [];
    public int HistoryReads { get; private set; }
    public Exception? FetchException { get; set; }
    public Exception? HistoryException { get; set; }
    public Exception? ReactionException { get; set; }
    public bool UseEmbeddedTargets { get; set; }
    public ITextChannel Channel { get; }
    public IDiscordClient RestClient { get; }
    public int RestClientDisposals { get; private set; }

    public DiscordConversationFixture()
    {
        object? ChannelCall(MethodInfo method, object?[]? args) =>
            method.Name switch
            {
                "get_Id" => ChannelId,
                "get_CategoryId" => (ulong?)null,
                "GetMessagesAsync" => GetBatches((ulong)args![0]!, (int)args[2]!, (RequestOptions?)args[^1]),
                "GetMessageAsync" => Fetch((ulong)args![0]!),
                "SendMessageAsync" => Send((string)args![0]!),
                "EnterTypingState" => new NoopDisposable(),
                _ => throw new NotSupportedException(method.Name),
            };
        var historyChannel = Stub<ITextChannel>(ChannelCall);
        Channel = Stub<ITextChannel>(
            (method, args) =>
                method.Name is "GetMessagesAsync" or "GetMessageAsync"
                    ? throw new InvalidOperationException("Socket history must not be used for boundary checks")
                    : ChannelCall(method, args)
        );
        RestClient = Stub<IDiscordClient>(
            (method, args) =>
            {
                if (method.Name is "Dispose" or "DisposeAsync")
                {
                    RestClientDisposals++;
                    return method.Name == "DisposeAsync" ? ValueTask.CompletedTask : null;
                }
                return method.Name == "GetChannelAsync" && (ulong)args![0]! == ChannelId
                    ? Task.FromResult<IChannel>(historyChannel)
                    : throw new NotSupportedException(method.Name);
            }
        );
    }

    public LorekeeperChatHistoryBuilder CreateBuilder() =>
        new(
            new SharedDiscordRestClient(RestClient),
            Options.Create(new DirectMessageHandlerOptions { AdminUserId = AdminId }),
            NullLogger<LorekeeperChatHistoryBuilder>.Instance
        );

    public IUserMessage AddMessage(
        ulong id,
        string content,
        ulong authorId = 2,
        string authorName = "Raider",
        ulong? replyTo = null,
        ulong referenceChannelId = ChannelId,
        IReadOnlyCollection<IAttachment>? attachments = null,
        DateTimeOffset? timestamp = null,
        bool authorIsBot = false
    )
    {
        var author = Stub<IUser>(
            (method, _) =>
                method.Name switch
                {
                    "get_Id" => authorId,
                    "get_IsBot" => authorIsBot || authorId == BotId,
                    "get_GlobalName" or "get_Username" => authorName,
                    _ => throw new NotSupportedException(method.Name),
                }
        );
        var reference = replyTo is null ? null : new MessageReference(replyTo, referenceChannelId);
        var message = Stub<IUserMessage>(
            (method, args) =>
                method.Name switch
                {
                    "get_Id" => id,
                    "get_Channel" => Channel,
                    "get_Author" => author,
                    "get_Content" or "get_CleanContent" => content,
                    "get_Timestamp" => timestamp ?? DateTimeOffset.UtcNow,
                    "get_Reference" => reference,
                    "get_ReferencedMessage" => UseEmbeddedTargets && replyTo is not null ? Messages.GetValueOrDefault(replyTo.Value) : null,
                    "get_Attachments" => attachments ?? Array.Empty<IAttachment>(),
                    "get_MentionedUserIds" => new[] { BotId },
                    "AddReactionAsync" => React((IEmote)args![0]!),
                    _ => throw new NotSupportedException(method.Name),
                }
        );
        Messages[id] = message;
        return message;
    }

    public static T Stub<T>(Func<MethodInfo, object?[]?, object?> invoke)
        where T : class
    {
        var proxy = DispatchProxy.Create<T, DiscordTestProxy>();
        ((DiscordTestProxy)(object)proxy).InvokeMethod = invoke;
        return proxy;
    }

    private async IAsyncEnumerable<IReadOnlyCollection<IMessage>> GetBatches(ulong beforeId, int count, RequestOptions? options)
    {
        options?.CancelToken.ThrowIfCancellationRequested();
        HistoryReads++;
        if (HistoryException is not null && HistoryReads > 1)
            throw HistoryException;
        await Task.CompletedTask;
        yield return Messages
            .Values.Where(message => message.Id < beforeId)
            .OrderByDescending(message => message.Id)
            .Take(count)
            .Cast<IMessage>()
            .ToArray();
    }

    private Task<IMessage> Fetch(ulong id)
    {
        FetchedIds.Add(id);
        return FetchException is null ? Task.FromResult<IMessage>(Messages.GetValueOrDefault(id)!) : Task.FromException<IMessage>(FetchException);
    }

    private Task React(IEmote emote)
    {
        if (ReactionException is not null)
            return Task.FromException(ReactionException);
        Reactions.Add(emote.Name);
        return Task.CompletedTask;
    }

    private Task<IUserMessage> Send(string content)
    {
        Replies.Add(content);
        return Task.FromResult<IUserMessage>(null!);
    }

    private sealed class NoopDisposable : IDisposable
    {
        public void Dispose() { }
    }
}
