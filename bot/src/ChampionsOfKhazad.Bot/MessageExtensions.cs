using Discord;

namespace ChampionsOfKhazad.Bot;

public static class MessageExtensions
{
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
}
