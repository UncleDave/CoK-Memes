namespace ChampionsOfKhazad.Bot.Roster.Blizzard;

public class BlizzardRosterOptions
{
    public const string Key = "Blizzard";
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string Region { get; set; } = "eu";
    public string GuildName { get; set; } = "Crossroads Mega Chippy";
    public string GuildSlug { get; set; } = "crossroads-mega-chippy";
    public string RosterPathTemplate { get; set; } = "/data/wow/guild/{guild}/roster";
    public string ProfessionPathTemplate { get; set; } = "/profile/wow/character/{first}-{last}/professions";
    public string ApiHost => $"{Region}.api.blizzard.com";
    public string ProfileNamespace => $"profile-forever-{Region}";
    public string StaticNamespace => $"static-forever-{Region}";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ClientId) || string.IsNullOrWhiteSpace(ClientSecret))
            throw new ArgumentException("Blizzard:ClientId and Blizzard:ClientSecret are required when the guild roster is enabled.");
        if (Region is not ("eu" or "us" or "tw" or "kr"))
            throw new ArgumentException("Blizzard:Region must be eu, us, tw, or kr.");
        if (
            string.IsNullOrWhiteSpace(GuildName)
            || GuildName.Length > 100
            || GuildName.Any(char.IsControl)
            || string.IsNullOrWhiteSpace(GuildSlug)
            || !System.Text.RegularExpressions.Regex.IsMatch(GuildSlug, @"\A[\p{L}\p{N}][\p{L}\p{M}\p{N}-]{0,127}\z")
        )
            throw new ArgumentException("Blizzard guild name and slug must identify the intended in-game guild.");
        ValidateTemplate(RosterPathTemplate, "/data/wow/guild/", "/roster", ["{guild}"]);
        ValidateTemplate(ProfessionPathTemplate, "/profile/wow/character/", "/professions", ["{first}", "{last}"]);
    }

    private static void ValidateTemplate(string template, string prefix, string suffix, string[] placeholders)
    {
        if (
            string.IsNullOrWhiteSpace(template)
            || !template.StartsWith(prefix, StringComparison.Ordinal)
            || !template.EndsWith(suffix, StringComparison.Ordinal)
        )
            throw new ArgumentException("Blizzard roster/profession path templates must use the expected resource paths.");
        foreach (var placeholder in placeholders)
        {
            if (!template.Contains(placeholder, StringComparison.Ordinal))
                throw new ArgumentException("Blizzard path templates are missing a required placeholder.");
            template = template.Replace(placeholder, "target", StringComparison.Ordinal);
        }
        if (
            template.Length > 500
            || template.Contains("..", StringComparison.Ordinal)
            || template.Contains("//", StringComparison.Ordinal)
            || template.Any(character => !(char.IsLetterOrDigit(character) || character is '/' or '-' or '_'))
        )
            throw new ArgumentException("Blizzard path templates must be paths, not URLs, queries, or fragments.");
    }
}
