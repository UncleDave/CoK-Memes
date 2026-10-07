using Discord;

namespace ChampionsOfKhazad.Bot;

public class TargetRoleFollowerTriggerStrategy(ulong roleId) : IFollowerTriggerStrategy
{
    public bool ShouldTrigger(MessageReceived notification) =>
        notification.Message.Author is IGuildUser guildUser && guildUser.RoleIds.Contains(roleId);
}
