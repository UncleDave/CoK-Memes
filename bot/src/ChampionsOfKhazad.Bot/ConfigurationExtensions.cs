using Microsoft.Extensions.Configuration;

namespace ChampionsOfKhazad.Bot;

public static class ConfigurationExtensions
{
    public static IConfigurationSection GetEventHandlerSection(
        this IConfiguration configuration,
        string key
    ) => configuration.GetSection($"{EventHandlerOptions.Key}:{key}");

    public static IConfigurationSection GetSlashCommandSection(
        this IConfiguration configuration,
        string key
    ) => configuration.GetSection($"{SlashCommandOptions.Key}:{key}");
}
