using System.Text.RegularExpressions;
using Discord;

namespace ChampionsOfKhazad.Bot;

public static class MessageExtensions
{
    private static readonly Regex NameExpression = new("^[a-zA-Z0-9_-]{1,64}$", RegexOptions.Compiled);

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

    public static string GetFriendlyAuthorName(this IMessage message) =>
        message.Author is IGuildUser { DisplayName: not null } guildUser && NameExpression.IsMatch(guildUser.DisplayName)
            ? guildUser.DisplayName
            : message.Author.GlobalName is not null && NameExpression.IsMatch(message.Author.GlobalName)
                ? message.Author.GlobalName
                : message.Author.Username is not null && NameExpression.IsMatch(message.Author.Username)
                    ? message.Author.Username
                    : message.Author.Id.ToString();
}
