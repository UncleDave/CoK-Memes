using ChampionsOfKhazad.Bot.GenAi;
using Discord;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ChampionsOfKhazad.Bot;

/// <summary>
/// TEMPORARY: Test handler to validate that IMessageContext is properly injected for ReactionAdded notifications.
/// This handler will fail if IMessageContext is not available in the current scope.
/// TODO: Remove this handler once the refactoring is validated in production.
/// </summary>
public class ReactionContextTestHandler(
    ILogger<ReactionContextTestHandler> logger,
    IMessageContext messageContext) : INotificationHandler<ReactionAdded>
{
    public async Task Handle(ReactionAdded notification, CancellationToken cancellationToken)
    {
        // This handler only processes reactions to messages that contain "!test-reaction"
        var reaction = notification.Reaction;
        var message = reaction.Message.IsSpecified ? reaction.Message.Value : await reaction.Channel.GetMessageAsync(reaction.MessageId);
        
        if (message?.Content?.Contains("!test-reaction", StringComparison.OrdinalIgnoreCase) != true)
            return;

        // Only process specific test emoji
        if (reaction.Emote.Name != "✅" && reaction.Emote.Name != "✨")
            return;

        try
        {
            // Test that the injected message context has the correct user ID (the message author, not the reactor)
            if (messageContext.UserId != message.Author.Id)
            {
                logger.LogError(
                    "Reaction context user ID mismatch! Expected: {ExpectedUserId}, Actual: {ActualUserId}",
                    message.Author.Id,
                    messageContext.UserId
                );
                return;
            }

            // Test that the injected message context can reply (to the original message)
            await messageContext.Reply($"✅ MessageContext DI working for reactions! Reacted by <@{reaction.UserId}> with {reaction.Emote}");
            
            logger.LogInformation(
                "Successfully validated MessageContext DI for reaction by user {ReactorUserId} on message from {AuthorUserId}",
                reaction.UserId,
                messageContext.UserId
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to validate MessageContext DI for reaction");
            if (message is IUserMessage userMessage)
            {
                await userMessage.ReplyAsync("❌ MessageContext dependency injection failed for reaction!");
            }
        }
    }
}