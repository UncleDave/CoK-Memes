using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace ChampionsOfKhazad.Bot.Portal;

public class AdminAuthorizationHandler(AuthOptions authOptions) : IAuthorizationHandler
{
    public Task HandleAsync(AuthorizationHandlerContext context)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (userId is null || !authOptions.UserIsAdmin(userId))
        {
            return Task.CompletedTask;
        }

        foreach (var requirement in context.Requirements)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
