using System.Text.RegularExpressions;
using Discord;

namespace ChampionsOfKhazad.Bot;

internal static partial class LorekeeperContextMarker
{
    public static bool IsMatch(IMessage message, ulong botId, ulong adminUserId)
    {
        if (message.Author.IsBot || message.Author.Id != adminUserId)
            return false;

        var match = MarkerExpression().Match(message.Content.Trim());
        return match.Success && match.Groups["botId"].Value == botId.ToString();
    }

    [GeneratedRegex("^<@!?(?<botId>\\d+)>\\s+you['’]ve had a stroke\\.?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MarkerExpression();
}
