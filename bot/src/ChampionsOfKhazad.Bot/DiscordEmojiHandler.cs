using System.Text.RegularExpressions;
using ChampionsOfKhazad.Bot.GenAi;

namespace ChampionsOfKhazad.Bot;

public class DiscordEmojiHandler(BotContext botContext) : IEmojiHandler
{
    private static readonly Regex EmojiExpression = new(@":(?<name>\w+):", RegexOptions.Compiled);

    public IEnumerable<string> GetEmojis() => botContext.Guild.Emotes.Select(e => e.Name);

    public string ProcessMessage(string message)
    {
        var matches = EmojiExpression.Matches(message);

        foreach (Match match in matches)
        {
            var emojiName = match.Groups["name"].Value;

            if (
                botContext.Guild.Emotes.FirstOrDefault(x => string.Equals(x.Name, emojiName, StringComparison.InvariantCultureIgnoreCase)) is
                { } emoji
            )
                message = message.Replace(match.Value, emoji.ToString());
        }

        return message;
    }
}
