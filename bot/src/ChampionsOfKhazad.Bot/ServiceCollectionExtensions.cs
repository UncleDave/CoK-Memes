using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ChampionsOfKhazad.Bot;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEventHandler<T>(this IServiceCollection services)
        where T : class, IEventHandler => services.AddSingleton<IEventHandler, T>();

    public static IServiceCollection AddEventHandler<TImplementation, TOptions>(
        this IServiceCollection services,
        IConfiguration configuration
    )
        where TImplementation : class, IEventHandler
        where TOptions : class =>
        services
            .AddSingleton(
                serviceProvider =>
                    EventHandlerFactory.CreateEventHandler<TImplementation>(
                        serviceProvider,
                        configuration
                    )
            )
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
