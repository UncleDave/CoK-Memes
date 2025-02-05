namespace ChampionsOfKhazad.Bot.DiscordMemes.CharacterDeaths;

public record CharacterDeath(
    ulong UserId,
    string CharacterName,
    DateTimeOffset Timestamp,
    string Obituary,
    ushort? Level,
    string? Race,
    string? Class,
    string? CauseOfDeath
);
