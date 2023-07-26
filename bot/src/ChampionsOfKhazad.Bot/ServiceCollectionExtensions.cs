using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ChampionsOfKhazad.Bot;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMessageReceivedEventHandler<T>(
        this IServiceCollection services
    )
        where T : class, IMessageReceivedEventHandler =>
        services.AddScoped<IMessageReceivedEventHandler, T>();

    public static IServiceCollection AddMessageReceivedEventHandler<TImplementation, TOptions>(
        this IServiceCollection services,
        IConfiguration configuration
    )
        where TImplementation : class, IMessageReceivedEventHandler
        where TOptions : class =>
        services
            .AddScoped(
                serviceProvider =>
                    EventHandlerFactory.CreateMessageReceivedEventHandler<TImplementation>(
                        serviceProvider,
                        configuration
                    )
            )
            .AddOptionsWithEagerValidation<TOptions>(configuration);

    public static IServiceCollection AddReactionAddedEventHandler<TImplementation, TOptions>(
        this IServiceCollection services,
        IConfiguration configuration
    )
        where TImplementation : class, IReactionAddedEventHandler
        where TOptions : class =>
        services
            .AddScoped<IReactionAddedEventHandler, TImplementation>()
            .AddOptionsWithEagerValidation<TOptions>(configuration);

    public static IServiceCollection AddSlashCommand<TImplementation, TOptions>(
        this IServiceCollection services,
        IConfiguration configuration
    )
        where TImplementation : class, ISlashCommand
        where TOptions : class =>
        services
            .AddScoped<TImplementation>()
            .AddOptionsWithEagerValidation<TOptions>(configuration);

    public static IServiceCollection AddOptionsWithEagerValidation<T>(
        this IServiceCollection services,
        IConfiguration configuration
    )
        where T : class
    {
        services.AddOptions<T>().Bind(configuration).ValidateDataAnnotations().ValidateOnStart();
        return services;
    }
}
