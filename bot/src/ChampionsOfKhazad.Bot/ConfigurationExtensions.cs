using Microsoft.Extensions.Configuration;

namespace ChampionsOfKhazad.Bot;

public static class ConfigurationExtensions
{
    public static IConfigurationSection GetEventHandlerSection(this IConfiguration configuration, string key) =>
        configuration.GetSection($"{EventHandlerOptions.Key}:{key}");

    public static IConfigurationSection GetSlashCommandSection(this IConfiguration configuration, string key) =>
        configuration.GetSection($"{SlashCommandOptions.Key}:{key}");

    public static string GetRequiredString(this IConfiguration configuration, string key) =>
        configuration[key] ?? throw new ApplicationException($"{key} is required");

    public static string GetRequiredConnectionString(this IConfiguration configuration, string key) =>
        configuration.GetConnectionString(key) ?? throw new ApplicationException($"ConnectionStrings:{key} is required");
}
