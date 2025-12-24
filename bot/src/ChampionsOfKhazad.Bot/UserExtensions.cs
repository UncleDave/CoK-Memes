using Discord;

namespace ChampionsOfKhazad.Bot;

public static class UserExtensions
{
    public static string GetName(this IUser user) =>
        user is IGuildUser { DisplayName: not null } guildUser
            ? guildUser.DisplayName
            : user.GlobalName ?? user.Username ?? user.Id.ToString();
}
