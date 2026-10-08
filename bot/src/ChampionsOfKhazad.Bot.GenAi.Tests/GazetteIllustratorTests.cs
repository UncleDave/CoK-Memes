using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;
using System.Text.Json;
using OpenAI;
using OpenAI.Images;

namespace ChampionsOfKhazad.Bot.GenAi.Tests;

public class GazetteIllustratorTests
{
    [Fact]
    public async Task ImageSdkSendsExplicitFlareQualityAndFormatWithoutLegacyResponseFormat()
    {
        using var handler = new CapturingImageHandler();
        using var http = new HttpClient(handler);
        var client = new ImageClient(
            Constants.DefaultImageModel,
            new ApiKeyCredential("unused-test-key"),
            new OpenAIClientOptions { Transport = new HttpClientPipelineTransport(http) }
        );
        await Assert.ThrowsAsync<ClientResultException>(() =>
            client.GenerateImageAsync("A simple visual gag.", GazetteIllustrator.CreateOptions(), TestContext.Current.CancellationToken)
        );
        using var request = JsonDocument.Parse(handler.Body!);
        var root = request.RootElement;
        Assert.Equal("gpt-image-2.5-flare", root.GetProperty("model").GetString());
        Assert.Equal("high", root.GetProperty("quality").GetString());
        Assert.Equal("1024x1024", root.GetProperty("size").GetString());
        Assert.Equal("png", root.GetProperty("output_format").GetString());
        Assert.False(root.TryGetProperty("response_format", out _));
    }

    [Fact]
    public void ArtPromptIsWordlessFictionalAndTreatsConceptAsQuotedData()
    {
        var prompt = GazetteIllustrator.BuildPrompt("A ceremonial phone resurrection.");
        Assert.Contains("NO text", prompt);
        Assert.Contains("untrusted subject data", prompt);
        Assert.Contains("identifiable real people", prompt);
        Assert.Contains("340-pixel thumbnail", prompt);
        Assert.Contains("ONE obvious visual punchline", prompt);
        Assert.Contains("two or three large essential props", prompt);
        Assert.Contains("Thick clean pen contours", prompt);
        Assert.Contains("no detailed rooms/workshops", prompt);
        Assert.Contains("A ceremonial phone resurrection.", prompt);
    }

    [Fact]
    public void GazetteUsesExplicitHighQualitySquarePngWithoutSwitchingTheSharedFlareModel()
    {
        var options = GazetteIllustrator.CreateOptions();
        Assert.Equal(new GeneratedImageQuality("high"), options.Quality);
        Assert.NotEqual(GeneratedImageQuality.High, options.Quality);
        Assert.Equal(GeneratedImageSize.W1024xH1024, options.Size);
#pragma warning disable OPENAI001
        Assert.Equal(GeneratedImageFileFormat.Png, options.OutputFileFormat);
#pragma warning restore OPENAI001
        Assert.Equal("gpt-image-2.5-flare", Constants.DefaultImageModel);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void EmptyConceptsNeverBecomeImageRequests(string concept) =>
        Assert.Throws<InvalidOperationException>(() => GazetteIllustrator.BuildPrompt(concept));

    private sealed class CapturingImageHandler : HttpMessageHandler
    {
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(
                    """{"error":{"message":"Controlled test rejection","type":"invalid_request_error","code":"invalid_value","param":"prompt"}}"""
                ),
            };
        }
    }
}
