using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;
using Azure.Core.Pipeline;
using Azure.Storage.Blobs;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAI;
using OpenAI.Images;

namespace ChampionsOfKhazad.Bot.GenAi.Tests;

public class GeneratedImageReplyTests
{
    [Fact]
    public async Task MissingImageLinkIsAppendedToTheNaturalReply()
    {
        using var fixture = new Fixture(_ => "Aye, here's your masterpiece.");

        var reply = await fixture.InvokeAsync();

        var image = Assert.Single(fixture.Store.Images);
        var imageUrl = $"{Constants.GeneratedImagesBaseUrl}/{image.Filename}";
        Assert.Equal($"Aye, here's your masterpiece.\n\n{imageUrl}", reply);
        Assert.Equal("A dwarf with a newspaper", image.Prompt);
        Assert.Equal(fixture.Context.UserId, image.UserId);
        Assert.Equal(1, fixture.Handler.GenerationRequests);
        Assert.Equal(image.Filename, fixture.Handler.UploadedFilename);
        Assert.StartsWith(Constants.ImageGenerationConfirmationMessage, Assert.Single(fixture.Context.Replies));
    }

    [Fact]
    public async Task ImageLinkAlreadyInTheNaturalReplyIsNotRepeated()
    {
        using var fixture = new Fixture(url => $"Behold [your masterpiece]({url}), you muppet.");

        var reply = await fixture.InvokeAsync();

        var image = Assert.Single(fixture.Store.Images);
        Assert.Equal($"Behold [your masterpiece]({Constants.GeneratedImagesBaseUrl}/{image.Filename}), you muppet.", reply);
    }

    [Fact]
    public async Task EmptyModelReplyStillIncludesTheGeneratedImage()
    {
        using var fixture = new Fixture(_ => "");

        var reply = await fixture.InvokeAsync();

        var image = Assert.Single(fixture.Store.Images);
        Assert.Equal($"{Constants.GeneratedImagesBaseUrl}/{image.Filename}", reply);
    }

    [Fact]
    public async Task GeneratedImagesDoNotLeakIntoLaterRequests()
    {
        using var fixture = new Fixture(_ => "Aye, here's your masterpiece.");
        await fixture.InvokeAsync();
        fixture.ChatClient.GenerateImage = false;

        var reply = await fixture.InvokeAsync();

        Assert.Equal("Aye, here's your masterpiece.", reply);
        Assert.Single(fixture.Store.Images);
        Assert.Equal(1, fixture.Handler.GenerationRequests);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(2, 2)]
    public async Task DeniedGenerationDoesNotAddAnImageLink(short allowance, ushort generatedCount)
    {
        using var fixture = new Fixture(_ => "No image for you.", allowance, generatedCount);

        var reply = await fixture.InvokeAsync();

        Assert.Equal("No image for you.", reply);
        Assert.Empty(fixture.Context.Replies);
        Assert.Empty(fixture.Store.Images);
        Assert.Equal(0, fixture.Handler.GenerationRequests);
    }

    [Fact]
    public async Task FailedGenerationDoesNotAddAnImageLink()
    {
        using var fixture = new Fixture(_ => "Image generation failed.");
        fixture.Handler.FailGeneration = true;

        var reply = await fixture.InvokeAsync();

        Assert.Equal("Image generation failed.", reply);
        Assert.StartsWith(Constants.ImageGenerationConfirmationMessage, Assert.Single(fixture.Context.Replies));
        Assert.Empty(fixture.Store.Images);
        Assert.Null(fixture.Handler.UploadedFilename);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly HttpClient _http;
        private readonly IPersonality _personality;

        public ImageHandler Handler { get; } = new();
        public MemoryImageStore Store { get; }
        public TestMessageContext Context { get; } = new();
        public ImageToolChatClient ChatClient { get; }

        public Fixture(Func<string?, string> modelReply, short allowance = 2, ushort generatedCount = 0)
        {
            _http = new HttpClient(Handler);
            Store = new MemoryImageStore { GeneratedCount = generatedCount };
            ChatClient = new ImageToolChatClient(Store, modelReply);
            var service = new ImageGenerationService(
                new GenAiImageGenerationConfig { DailyAllowances = new() { [1] = allowance } },
                Store,
                new ImageClient(
                    Constants.DefaultImageModel,
                    new ApiKeyCredential("unused-test-key"),
                    new OpenAIClientOptions { Transport = new HttpClientPipelineTransport(_http) }
                ),
                new ImageStorageService(
                    new BlobServiceClient(
                        new Uri("https://storage.example.test"),
                        new BlobClientOptions { Transport = new HttpClientTransport(_http) }
                    )
                ),
                NullLogger<ImageGenerationService>.Instance
            );
            var tools = new PersonalityTools(null!, service, null!, null!, NullLogger<PersonalityTools>.Instance);
            _personality = new TestPersonality(new ChatClientBuilder(ChatClient).UseFunctionInvocation().Build(), tools);
        }

        public Task<string> InvokeAsync() =>
            _personality.InvokeAsync(
                new ChatHistory([new ChatMessage(ChatRole.User, "Draw a dwarf with a newspaper")]),
                Context,
                TestContext.Current.CancellationToken
            );

        public void Dispose()
        {
            ChatClient.Dispose();
            _http.Dispose();
        }
    }

    private sealed class TestPersonality(IChatClient chatClient, PersonalityTools tools)
        : PersonalityBase("Test lorekeeper", true, new PassThroughEmojiHandler(), chatClient, tools);

    private sealed class PassThroughEmojiHandler : IEmojiHandler
    {
        public IEnumerable<string> GetEmojis() => [];

        public string ProcessMessage(string message) => message;
    }

    private sealed class ImageToolChatClient(MemoryImageStore store, Func<string?, string> modelReply) : IChatClient
    {
        public bool GenerateImage { get; set; } = true;

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default
        )
        {
            if (GenerateImage && messages.Last().Role != ChatRole.Tool)
                return Task.FromResult(
                    new ChatResponse(
                        new ChatMessage(
                            ChatRole.Assistant,
                            [
                                new FunctionCallContent(
                                    "image-call",
                                    "generate_image",
                                    new Dictionary<string, object?> { ["prompt"] = "A dwarf with a newspaper" }
                                ),
                            ]
                        )
                    )
                    {
                        FinishReason = ChatFinishReason.ToolCalls,
                    }
                );

            var imageUrl = store.Images.LastOrDefault() is { } image ? $"{Constants.GeneratedImagesBaseUrl}/{image.Filename}" : null;
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, modelReply(imageUrl))));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default
        )
        {
            await Task.CompletedTask;
            yield break;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }

    private sealed class TestMessageContext : IMessageContext
    {
        public ulong UserId => 1;
        public string UserName => "Tester";
        public ulong? ChannelId => 3;
        public List<string> Replies { get; } = [];

        public Task Reply(string message)
        {
            Replies.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class MemoryImageStore : IGeneratedImageStore
    {
        public ushort GeneratedCount { get; init; }
        public List<GeneratedImage> Images { get; } = [];

        public Task<IReadOnlyCollection<GeneratedImage>> GetAsync(
            ushort skip = 0,
            ushort take = 20,
            ulong? userId = null,
            bool sortAscending = false,
            string? searchText = null,
            CancellationToken cancellationToken = default
        ) => Task.FromResult<IReadOnlyCollection<GeneratedImage>>(Images);

        public Task<ushort> GetDailyGeneratedImageCountAsync(ulong userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(GeneratedCount);

        public Task SaveGeneratedImageAsync(GeneratedImage image)
        {
            Images.Add(image);
            return Task.CompletedTask;
        }
    }

    private sealed class ImageHandler : HttpMessageHandler
    {
        public bool FailGeneration { get; set; }
        public int GenerationRequests { get; private set; }
        public string? UploadedFilename { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath == "/v1/images/generations")
            {
                GenerationRequests++;
                return Task.FromResult(
                    new HttpResponseMessage(FailGeneration ? HttpStatusCode.BadRequest : HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            FailGeneration
                                ? """{"error":{"message":"Controlled test rejection","type":"invalid_request_error"}}"""
                                : """{"created":1,"data":[{"b64_json":"aW1hZ2U="}]}"""
                        ),
                    }
                );
            }

            Assert.Equal(HttpMethod.Put, request.Method);
            Assert.Equal("storage.example.test", request.RequestUri!.Host);
            if (request.RequestUri.Query.Length == 0)
                UploadedFilename = request.RequestUri.Segments[^1];

            var response = new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent("") };
            response.Headers.Add("ETag", "\"test-etag\"");
            response.Content.Headers.LastModified = new DateTimeOffset(2026, 10, 8, 0, 0, 0, TimeSpan.Zero);
            return Task.FromResult(response);
        }
    }
}
