using ChampionsOfKhazad.Bot.Core;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public class DiscordStatsBuilder(IServiceCollection services, BotConfiguration botConfiguration) : BotBuilder(services, botConfiguration);
