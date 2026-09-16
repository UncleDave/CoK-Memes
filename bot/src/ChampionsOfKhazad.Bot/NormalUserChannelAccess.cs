using Discord;

namespace ChampionsOfKhazad.Bot;

internal static class NormalUserChannelAccess
{
    public static bool CanRead(IGuildChannel channel, IReadOnlyCollection<IRole> roles)
    {
        var everyoneRole = roles.FirstOrDefault(role => role.Id == channel.GuildId);

        return everyoneRole is not null
            && CanRead(
                roles.Select(role => role.Permissions),
                channel.GetPermissionOverwrite(everyoneRole),
                roles.Where(role => role.Id != everyoneRole.Id).Select(channel.GetPermissionOverwrite)
            );
    }

    internal static bool CanRead(
        IEnumerable<GuildPermissions> rolePermissions,
        OverwritePermissions? everyoneOverwrite,
        IEnumerable<OverwritePermissions?> roleOverwrites
    )
    {
        var permissionsByRole = rolePermissions.ToArray();

        if (permissionsByRole.Any(role => role.Administrator))
            return true;

        var permissions = permissionsByRole.Aggregate(0UL, (value, role) => value | role.RawValue);
        permissions = ApplyOverwrite(permissions, everyoneOverwrite);

        var roleAllow = 0UL;
        var roleDeny = 0UL;

        foreach (var overwrite in roleOverwrites.OfType<OverwritePermissions>())
        {
            roleAllow |= overwrite.AllowValue;
            roleDeny |= overwrite.DenyValue;
        }

        permissions = (permissions & ~roleDeny) | roleAllow;

        var channelPermissions = new ChannelPermissions(permissions);
        return channelPermissions is { ViewChannel: true, ReadMessageHistory: true };
    }

    private static ulong ApplyOverwrite(ulong permissions, OverwritePermissions? overwrite) =>
        overwrite is null ? permissions : (permissions & ~overwrite.Value.DenyValue) | overwrite.Value.AllowValue;
}
