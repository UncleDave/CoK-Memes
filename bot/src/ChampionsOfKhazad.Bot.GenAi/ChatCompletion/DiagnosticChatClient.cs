using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace ChampionsOfKhazad.Bot.GenAi;

internal sealed class DiagnosticChatClient(IChatClient innerClient, ILogger<DiagnosticChatClient> logger) : DelegatingChatClient(innerClient)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        var requestMessages = messages as IReadOnlyCollection<ChatMessage> ?? messages.ToArray();

        try
        {
            return await base.GetResponseAsync(requestMessages, options, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation(
                "Chat completion was cancelled for {MessageCount} messages with {ToolCount} tools.",
                requestMessages.Count,
                options?.Tools?.Count ?? 0
            );
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Chat completion failed for {MessageCount} messages with {ToolCount} tools.",
                requestMessages.Count,
                options?.Tools?.Count ?? 0
            );
            throw;
        }
    }
}
