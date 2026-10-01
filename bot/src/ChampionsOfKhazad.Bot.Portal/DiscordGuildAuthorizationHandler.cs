using Microsoft.AspNetCore.Authorization;

namespace ChampionsOfKhazad.Bot.Portal;

public class DiscordGuildAuthorizationHandler(DiscordUserResolver discordUserResolver) : AuthorizationHandler<DiscordGuildRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, DiscordGuildRequirement requirement)
    {
        if (!context.User.TryGetDiscordUserId(out var userId))
        {
            context.Fail();
            return;
        }

        var user = await discordUserResolver.GetGuildUserAsync(userId);

        if (user?.GuildId == requirement.GuildId)
        {
            context.Succeed(requirement);
        }
        else
        {
            context.Fail();
        }
    }
}
