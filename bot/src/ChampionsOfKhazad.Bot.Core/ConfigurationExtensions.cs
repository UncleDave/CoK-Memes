using Microsoft.Extensions.Configuration;

namespace ChampionsOfKhazad.Bot.Core;

public static class ConfigurationExtensions
{
    public static string GetRequiredString(this IConfiguration configuration, string key) =>
        configuration[key] ?? throw new ApplicationException($"{key} is required");

    public static string GetRequiredConnectionString(this IConfiguration configuration, string key) =>
        configuration.GetConnectionString(key) ?? throw new ApplicationException($"ConnectionStrings:{key} is required");
}
