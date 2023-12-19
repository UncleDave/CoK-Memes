using Microsoft.Extensions.Configuration;

namespace ChampionsOfKhazad.Bot;

public static class ConfigurationExtensions
{
    public static IConfigurationSection GetEventHandlerSection(this IConfiguration configuration, string key) =>
        configuration.GetSection($"EventHandlers:{key}");

    public static IConfigurationSection GetSlashCommandSection(this IConfiguration configuration, string key) =>
        configuration.GetSection($"SlashCommands:{key}");

    public static IConfigurationSection GetFollowerSection(this IConfiguration configuration, string key) =>
        configuration.GetSection($"{AllFollowersOptions.Key}:{key}");
}
