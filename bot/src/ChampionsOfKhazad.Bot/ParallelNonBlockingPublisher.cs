using ChampionsOfKhazad.Bot.GenAi;
using Discord;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ChampionsOfKhazad.Bot;

public class ParallelNonBlockingPublisher(
    ILogger<ParallelNonBlockingPublisher> logger,
    IServiceProvider rootServiceProvider) : INotificationPublisher
{
    public Task Publish(IEnumerable<NotificationHandlerExecutor> handlerExecutors, INotification notification, CancellationToken cancellationToken)
    {
        var messageContext = ExtractMessageContext(notification);

        foreach (var handler in handlerExecutors)
        {
            Task.Run(
                async () =>
                {
                    // Create a child scope for each handler
                    using var scope = rootServiceProvider.CreateScope();
                    
                    // Set the message context in the child scope if available
                    if (messageContext is not null)
                    {
                        var contextProvider = scope.ServiceProvider.GetRequiredService<MessageContextProvider>();
                        contextProvider.MessageContext = messageContext;
                    }

                    try
                    {
                        await handler.HandlerCallback(notification, cancellationToken);
                    }
                    catch (Exception e)
                    {
                        logger.LogError(
                            e,
                            "Error handling notification {NotificationType} in handler {HandlerType}",
                            notification.GetType().Name,
                            handler.HandlerInstance.GetType().Name
                        );
                    }
                },
                cancellationToken
            );
        }

        return Task.CompletedTask;
    }

    private static IMessageContext? ExtractMessageContext(INotification notification)
    {
        return notification switch
        {
            MessageReceived messageReceived => messageReceived.Message.ToMessageContext(),
            ReactionAdded reactionAdded when reactionAdded.Reaction.Message.IsSpecified 
                && reactionAdded.Reaction.Message.Value is IUserMessage userMessage => userMessage.ToMessageContext(),
            _ => null
        };
    }
}
