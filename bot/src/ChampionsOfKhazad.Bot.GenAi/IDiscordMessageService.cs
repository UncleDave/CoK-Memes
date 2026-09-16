namespace ChampionsOfKhazad.Bot.GenAi;

public interface IDiscordMessageService
{
    Task<string> FindChannelsAsync(string? query, IMessageContext messageContext, CancellationToken cancellationToken);

    Task<string> SearchMessagesAsync(
        string query,
        string? channelReference,
        int limit,
        IMessageContext messageContext,
        CancellationToken cancellationToken
    );

    Task<string> ReadMessagesAsync(
        string channelReference,
        ulong? beforeMessageId,
        int limit,
        IMessageContext messageContext,
        CancellationToken cancellationToken
    );
}
