// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class DiscordMemesBotBuilderExtensions
{
    public static DiscordMemesBuilder AddDiscordMemes(this BotBuilder builder)
    {
        builder.Services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssemblyContaining<DiscordMemesBuilder>();
        });

        return new DiscordMemesBuilder(builder.Services, builder.BotConfiguration);
    }
}
