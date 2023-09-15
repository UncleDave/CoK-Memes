using Microsoft.Extensions.Configuration;

namespace ChampionsOfKhazad.Bot;

public static class ConfigurationExtensions
{
    public static IConfigurationSection GetEventHandlerSection(this IConfiguration configuration, string key) =>
        configuration.GetSection($"EventHandlers:{key}");

    public static IConfigurationSection GetSlashCommandSection(this IConfiguration configuration, string key) =>
        configuration.GetSection($"SlashCommands:{key}");
    
    public static IConfigurationSection GetFollowerSection(this IConfiguration configuration, string key) =>
        configuration.GetSection($"Followers:{key}");

    public static string GetRequiredString(this IConfiguration configuration, string key) =>
        configuration[key] ?? throw new ApplicationException($"{key} is required");

    public static string GetRequiredConnectionString(this IConfiguration configuration, string key) =>
        configuration.GetConnectionString(key) ?? throw new ApplicationException($"ConnectionStrings:{key} is required");
}
