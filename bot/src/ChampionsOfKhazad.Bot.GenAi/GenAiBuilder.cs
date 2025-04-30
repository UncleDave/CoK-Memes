using ChampionsOfKhazad.Bot.Core;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public class GenAiBuilder(IServiceCollection services, BotConfiguration botConfiguration) : BotBuilder(services, botConfiguration);
