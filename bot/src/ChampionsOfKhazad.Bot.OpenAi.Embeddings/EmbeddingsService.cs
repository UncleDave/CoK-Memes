using System.Net.Http.Json;

namespace ChampionsOfKhazad.Bot.OpenAi.Embeddings;

public class EmbeddingsService(HttpClient httpClient)
{
    public async Task<float[]> CreateEmbeddingAsync(string input)
    {
        var request = new CreateEmbeddingsRequest([input]);
        var response = await httpClient.PostAsJsonAsync(request);

        response.EnsureSuccessStatusCode();

        var responseContent = await response.Content.ReadFromJsonAsync<CreateEmbeddingsResponse>();

        if (responseContent is null)
            throw new ApplicationException("Response content was null");

        return responseContent.Data.Single().Embedding;
    }

    private record EmbeddingResponse(float[] Embedding, int Index);

    private record CreateEmbeddingsResponse(EmbeddingResponse[] Data);

    private record CreateEmbeddingsRequest(IEnumerable<string> Input, string Model = Constants.Model);
}
