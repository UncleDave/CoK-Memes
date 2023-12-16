namespace ChampionsOfKhazad.Bot.Portal;

public record AuthOptions(string Domain, string ClientId, string ClientSecret, string[] AllowedUserIds);
