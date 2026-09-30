using System.Globalization;
using ChampionsOfKhazad.Bot.GenAi;

namespace ChampionsOfKhazad.Bot;

public class PersonalityDirectMessageCommand(LorekeeperPersonalityService personalityService)
{
    private const string HelpText =
        "Commands: personality | personality list | personality <baseline|grouchy|furious> [duration] | personality reset. Duration: whole minutes, hours, or days (e.g. 30m, 2h, 1d), up to 30 days; expires to baseline.";

    // Called only after the DM handler has verified the configured admin's identity.
    public async Task<string?> ExecuteAsync(string content, CancellationToken cancellationToken = default)
    {
        var parts = content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || !parts[0].Equals("personality", StringComparison.OrdinalIgnoreCase))
            return null;

        if (parts.Length == 1)
            return Describe(await personalityService.GetAsync(cancellationToken));

        if (parts.Length == 2 && parts[1].Equals("list", StringComparison.OrdinalIgnoreCase))
            return "baseline — original wise, helpful Lorekeeper\ngrouchy — first comic, rude persona\nfurious — intensified angry, foul-mouthed persona";

        if (parts.Length == 2 && parts[1].Equals("reset", StringComparison.OrdinalIgnoreCase))
            return Describe(await personalityService.SetAsync(LorekeeperTemperament.Baseline, cancellationToken: cancellationToken));

        var temperament = parts[1].ToLowerInvariant() switch
        {
            "baseline" => LorekeeperTemperament.Baseline,
            "grouchy" => LorekeeperTemperament.Grouchy,
            "furious" => LorekeeperTemperament.Furious,
            _ => (LorekeeperTemperament?)null,
        };

        if (temperament is null || parts.Length > 3)
            return HelpText;

        TimeSpan? duration = null;
        if (parts.Length == 3)
        {
            duration = ParseDuration(parts[2]);
            if (duration is null)
                return "Invalid duration. " + HelpText;
        }

        return Describe(await personalityService.SetAsync(temperament.Value, duration, cancellationToken));
    }

    private static TimeSpan? ParseDuration(string text)
    {
        if (text.Length < 2 || !int.TryParse(text[..^1], NumberStyles.None, CultureInfo.InvariantCulture, out var amount) || amount <= 0)
            return null;

        var minutesPerUnit = char.ToLowerInvariant(text[^1]) switch
        {
            'm' => 1,
            'h' => 60,
            'd' => 1440,
            _ => 0,
        };
        var minutes = (long)amount * minutesPerUnit;
        return minutes is > 0 and <= 43200 ? TimeSpan.FromMinutes(minutes) : null;
    }

    private static string Describe(LorekeeperPersonalitySetting setting) =>
        $"Lorekeeper personality: {setting.Temperament.ToString().ToLowerInvariant()}. "
        + (
            setting.ExpiresAtUtc is { } expiry
                ? $"Returns to baseline at {expiry.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture)}."
                : "Active until changed."
        );
}
