using ChampionsOfKhazad.Bot.GenAi;
using Discord;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ChampionsOfKhazad.Bot;

/// <summary>
/// TEMPORARY: Test handler to validate that IMessageContext is properly injected as a scoped dependency.
/// This handler will fail if IMessageContext is not available in the current scope.
/// TODO: Remove this handler once the refactoring is validated in production.
/// </summary>
public class MessageContextTestHandler(
    ILogger<MessageContextTestHandler> logger,
    IMessageContext messageContext) : INotificationHandler<MessageReceived>
{
    public async Task Handle(MessageReceived notification, CancellationToken cancellationToken)
    {
        // This handler only processes messages that contain "!test-context"
        if (!notification.Message.Content.Contains("!test-context", StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            // Test that the injected message context has the correct user ID
            if (messageContext.UserId != notification.Message.Author.Id)
            {
                logger.LogError(
                    "Message context user ID mismatch! Expected: {ExpectedUserId}, Actual: {ActualUserId}",
                    notification.Message.Author.Id,
                    messageContext.UserId
                );
                return;
            }

            // Test that the injected message context can reply
            await messageContext.Reply("✅ MessageContext dependency injection is working correctly!");
            
            logger.LogInformation(
                "Successfully validated MessageContext DI for user {UserId}",
                messageContext.UserId
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to validate MessageContext DI");
            await notification.Message.ReplyAsync("❌ MessageContext dependency injection failed!");
        }
    }
}