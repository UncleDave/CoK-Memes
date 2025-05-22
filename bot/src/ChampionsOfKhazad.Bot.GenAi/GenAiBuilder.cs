using ChampionsOfKhazad.Bot.Core;
using ChampionsOfKhazad.Bot.GenAi;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public class GenAiBuilder(IServiceCollection services, BotConfiguration botConfiguration) : BotBuilder(services, botConfiguration)
{
    public GenAiBuilder AddPromptEnricher<T>()
        where T : class, IPromptEnricher
    {
        Services.AddSingleton<IPromptEnricher, T>();

        return this;
    }
}
