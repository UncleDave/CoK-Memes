namespace ChampionsOfKhazad.Bot.LoreUploader;

public record Member(
    string Name,
    string? Aliases,
    string Pronouns,
    string Nationality,
    string? Roles,
    string MainCharacter,
    string Biography
);
