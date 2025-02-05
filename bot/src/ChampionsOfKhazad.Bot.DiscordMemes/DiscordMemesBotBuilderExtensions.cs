using ChampionsOfKhazad.Bot.DiscordMemes.WordOfTheDay;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class DiscordMemesBotBuilderExtensions
{
    public static DiscordMemesBuilder AddDiscordMemes(this BotBuilder builder)
    {
        builder
            .Services.AddMediatR(configuration =>
            {
                configuration.RegisterServicesFromAssemblyContaining<DiscordMemesBuilder>();
            })
            .AddSingleton<WordOfTheDayService>()
            .AddSingleton<IGetTheWordOfTheDay>(sp => sp.GetRequiredService<WordOfTheDayService>())
            .AddSingleton<IWinTheWordOfTheDay>(sp => sp.GetRequiredService<WordOfTheDayService>());

        return new DiscordMemesBuilder(builder.Services, builder.BotConfiguration);
    }
}
