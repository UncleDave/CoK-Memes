using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ChampionsOfKhazad.Bot;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOptionsWithEagerValidation<T>(this IServiceCollection services, IConfiguration configuration)
        where T : class
    {
        services.AddOptions<T>().Bind(configuration).ValidateDataAnnotations().ValidateOnStart();
        return services;
    }
}
