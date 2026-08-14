using System.Security.Claims;

namespace ChampionsOfKhazad.Bot.Portal;

public static class ClaimsPrincipalExtensions
{
    extension(ClaimsPrincipal claimsPrincipal)
    {
        public ulong GetDiscordUserId()
        {
            var nameIdentifier =
                claimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("NameIdentifier claim not found.");

            var splitNameIdentifier = nameIdentifier.Split('|');

            if (splitNameIdentifier.Length != 3)
            {
                throw new InvalidOperationException("Invalid NameIdentifier claim format.");
            }

            var userIdString = splitNameIdentifier[2];

            return !ulong.TryParse(userIdString, out var userId)
                ? throw new InvalidOperationException($"Invalid user ID format: {userIdString}")
                : userId;
        }

        public bool TryGetDiscordUserId(out ulong userId)
        {
            try
            {
                userId = claimsPrincipal.GetDiscordUserId();
                return true;
            }
            catch (InvalidOperationException)
            {
                userId = 0;
                return false;
            }
        }
    }
}
