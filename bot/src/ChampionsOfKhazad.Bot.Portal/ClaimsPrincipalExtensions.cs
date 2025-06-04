using System.Security.Claims;

namespace ChampionsOfKhazad.Bot.Portal;

public static class ClaimsPrincipalExtensions
{
    public static ulong GetDiscordUserId(this ClaimsPrincipal claimsPrincipal)
    {
        var nameIdentifier =
            claimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("NameIdentifier claim not found.");

        var splitNameIdentifier = nameIdentifier.Split('|');

        if (splitNameIdentifier.Length != 3)
        {
            throw new InvalidOperationException("Invalid NameIdentifier claim format.");
        }

        var userIdString = splitNameIdentifier[2];

        if (!ulong.TryParse(userIdString, out var userId))
        {
            throw new InvalidOperationException($"Invalid user ID format: {userIdString}");
        }

        return userId;
    }
}
