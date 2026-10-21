using System.Text;
using System.Text.Json.Serialization;
using ChampionsOfKhazad.Bot.Lore.Abstractions;

namespace ChampionsOfKhazad.Bot.Lore;

public record MemberLore(string Name, string Pronouns, string Nationality, string MainCharacter, string? Biography) : Lore(Name), IMemberLore
{
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public ulong? DiscordUserId { get; init; }
    public IReadOnlyList<string> Aliases { get; init; } = [];
    public IReadOnlyList<string> Roles { get; init; } = [];

    public static void ValidateDiscordUserId(ulong? discordUserId)
    {
        if (discordUserId is 0 or > long.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(discordUserId), "Discord user IDs must be between 1 and 9223372036854775807.");
    }

    public override string ToString() =>
        new StringBuilder($"Guild Member\n\nName: {Name}\n")
            .AppendIf(Aliases.Any(), $"Aliases: {string.Join(", ", Aliases)}\n")
            .Append($"Pronouns: {Pronouns}\nNationality: {Nationality}\n")
            .AppendIf(Roles.Any(), $"Roles: {string.Join(", ", Roles)}\n")
            .Append($"Main Character: {MainCharacter}\n")
            .AppendIf(!string.IsNullOrWhiteSpace(Biography), $"Biography: {Biography}\n")
            .ToString();
}
