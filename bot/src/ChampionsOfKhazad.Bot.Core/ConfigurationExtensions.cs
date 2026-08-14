using Microsoft.Extensions.Configuration;

namespace ChampionsOfKhazad.Bot.Core;

public static class ConfigurationExtensions
{
    extension(IConfiguration configuration)
    {
        public string GetRequiredString(string key)
        {
            var value = configuration[key];

            return !string.IsNullOrWhiteSpace(value) ? value : throw new ApplicationException($"{key} is required");
        }

        public string GetRequiredConnectionString(string key)
        {
            var value = configuration.GetConnectionString(key);

            return !string.IsNullOrWhiteSpace(value) ? value : throw new ApplicationException($"ConnectionStrings:{key} is required");
        }
    }
}
