using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;

namespace ChampionsOfKhazad.Bot.RaidHelper;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRaidHelperClient(this IServiceCollection services, string apiKey)
    {
        services.AddHttpClient<RaidHelperClient>(client =>
        {
            client.BaseAddress = new Uri("https://raid-helper.dev/api/");
            client.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse(apiKey);
        });

        return services;
    }
}
