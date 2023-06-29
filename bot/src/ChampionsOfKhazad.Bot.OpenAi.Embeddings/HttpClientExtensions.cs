using System.Net.Http.Json;

namespace ChampionsOfKhazad.Bot.OpenAi.Embeddings;

public static class HttpClientExtensions
{
    public static Task<HttpResponseMessage> PostAsJsonAsync<T>(
        this HttpClient httpClient,
        T content
    ) => httpClient.PostAsJsonAsync((string?)null, content);
}
