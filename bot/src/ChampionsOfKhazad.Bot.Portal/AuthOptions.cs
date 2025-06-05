namespace ChampionsOfKhazad.Bot.Portal;

public record AuthOptions(string Domain, string ClientId, string ClientSecret, string[] AdminUserIds)
{
    public bool UserIsAdmin(string userId) => AdminUserIds.Contains(userId);
}
