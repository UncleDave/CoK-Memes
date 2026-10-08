using Discord;

namespace ChampionsOfKhazad.Bot;

internal static class GazetteReadButton
{
    private const string Prefix = "gazette:read:";

    public static MessageComponent Build(string publicationId)
    {
        if (!TryParse(Prefix + publicationId, out _))
            throw new InvalidOperationException("Invalid Gazette publication button ID.");
        return new ComponentBuilder().WithButton("Read text & sources", Prefix + publicationId, ButtonStyle.Secondary).Build();
    }

    public static bool TryParse(string customId, out string publicationId)
    {
        publicationId = "";
        if (!customId.StartsWith(Prefix, StringComparison.Ordinal))
            return false;
        var suffix = customId[Prefix.Length..];
        if (suffix.Length != 12 || suffix.Any(character => character is not (>= '0' and <= '9' or >= 'a' and <= 'f')))
            return false;
        publicationId = suffix;
        return true;
    }

    // Components' normal DeferAsync acknowledges an update to the public message.
    // Loading creates a new, private reader response instead.
    public static Task AcknowledgeAsync(IComponentInteraction interaction) => interaction.DeferLoadingAsync(ephemeral: true);
}
