using ChampionsOfKhazad.Bot.DiscordMemes.WordOfTheDay;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class DiscordMemesBotBuilderExtensions
{
    public static BotBuilder AddDiscordMemes(this BotBuilder builder)
    {
        builder
            .Services.AddMediatR(configuration =>
            {
                configuration.RegisterServicesFromAssemblyContaining<WordOfTheDayService>();
            })
            .AddSingleton<WordOfTheDayService>()
            .AddSingleton<IGetTheWordOfTheDay>(sp => sp.GetRequiredService<WordOfTheDayService>())
            .AddSingleton<IWinTheWordOfTheDay>(sp => sp.GetRequiredService<WordOfTheDayService>());

        return builder;
    }
}
