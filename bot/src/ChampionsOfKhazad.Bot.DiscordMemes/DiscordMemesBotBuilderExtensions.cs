using ChampionsOfKhazad.Bot.DiscordMemes.StreakBreaks;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class DiscordMemesBotBuilderExtensions
{
    public static DiscordMemesBuilder AddDiscordMemes(this BotBuilder builder)
    {
        builder
            .Services.AddMediatR(configuration =>
            {
                configuration.RegisterServicesFromAssemblyContaining<StreakBreak>();
            });

        return new DiscordMemesBuilder(builder.Services, builder.BotConfiguration);
    }
}
