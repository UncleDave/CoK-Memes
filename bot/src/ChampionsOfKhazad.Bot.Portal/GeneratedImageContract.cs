namespace ChampionsOfKhazad.Bot.Portal;

public record GeneratedImageUserContract(string Name, string AvatarUrl);

public record GeneratedImageContract(string Prompt, GeneratedImageUserContract User, DateTimeOffset Timestamp, string Uri);
