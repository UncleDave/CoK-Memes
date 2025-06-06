namespace ChampionsOfKhazad.Bot.GenAi;

public record GeneratedImage(string Prompt, ulong UserId, DateTimeOffset Timestamp, string Filename);
