using ChampionsOfKhazad.Bot.EventLoop;
using Microsoft.Extensions.Configuration;

namespace ChampionsOfKhazad.Bot;

public static class ConfigurationExtensions
{
    extension(IConfiguration configuration)
    {
        public IConfigurationSection GetEventHandlerSection(string key) => configuration.GetSection($"EventHandlers:{key}");

        public IConfigurationSection GetSlashCommandSection(string key) => configuration.GetSection($"SlashCommands:{key}");

        public IConfigurationSection GetFollowerSection(string key) => configuration.GetSection($"{AllFollowersOptions.Key}:{key}");

        public IConfiguration GetEventLoopSection(string key) => configuration.GetSection($"{EventLoopOptions.Key}:{key}");
    }
}
