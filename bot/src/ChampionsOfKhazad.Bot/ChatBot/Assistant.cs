using ChampionsOfKhazad.Bot.OpenAi.Embeddings;
using ChampionsOfKhazad.Bot.Pinecone;
using Microsoft.Extensions.Logging;
using OpenAI.Interfaces;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels.ResponseModels;
using Pinecone;

namespace ChampionsOfKhazad.Bot.ChatBot;

public class Assistant
{
    private static readonly string Instructions = string.Join(
        '\n',
        "You are the Dwarf Lorekeeper of a \"World of Warcraft: Wrath of the Lich King\" guild known as Champions of Khazad.",
        "You know nothing about game content that was added after the expansion \"Wrath of the Lich King\".",
        "Your name is \"Lorekeeper Hammerstone\", users will also refer to you as \"CoK Bot\". Limit your replies to 100 words, and prefer shorter answers.",
        "You have a Dwarven accent."
    );

    private readonly IOpenAIService _openAiService;
    private readonly ILogger<Assistant> _logger;
    private readonly EmbeddingsService _embeddingsService;
    private readonly IndexService _pineconeIndexService;

    public Assistant(
        IOpenAIService openAiService,
        ILogger<Assistant> logger,
        EmbeddingsService embeddingsService,
        IndexService pineconeIndexService
    )
    {
        _openAiService = openAiService;
        _logger = logger;
        _embeddingsService = embeddingsService;
        _pineconeIndexService = pineconeIndexService;
    }

    public async Task<string> RespondAsync(
        string message,
        User user,
        IEnumerable<ChatMessage>? chatContext = null,
        ChatMessage? referencedMessage = null,
        string? instructions = null
    )
    {
        var embeddings = await _embeddingsService.CreateEmbeddingsAsync(
            new TextEntry("input", message)
        );
        var embedding = embeddings.SingleOrDefault();

        var vectorIndex = await _pineconeIndexService.GetIndexAsync("cok-lore");
        var vectors = embedding is not null
            ? await vectorIndex.Query(
                embedding.Vector,
                10,
                includeValues: false,
                includeMetadata: true
            )
            : Array.Empty<ScoredVector>();

        const string separator = "\n\n###\n\n";

        var messages = (chatContext ?? Array.Empty<ChatMessage>())
            .Prepend(
                ChatMessage.FromSystem(
                    string.Join(
                        separator,
                        string.Join(separator, vectors.Select(x => x.Metadata!["text"].Inner)),
                        instructions ?? Instructions
                    )
                )
            )
            .Append(ChatMessage.FromUser(message, user.Name))
            .ToList();

        if (referencedMessage is not null)
        {
            messages.Add(
                ChatMessage.FromSystem(
                    $"The user is referencing this message from \"{referencedMessage.Name}\": \"{referencedMessage.Content}\""
                )
            );
        }

        ChatCompletionCreateResponse result;

        try
        {
            result = await _openAiService.ChatCompletion.CreateCompletion(
                new ChatCompletionCreateRequest
                {
                    Messages = messages,
                    Model = Models.ChatGpt3_5Turbo,
                    MaxTokens = 500,
                    N = 1,
                    User = user.Id.ToString()
                }
            );
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Chat completion request failed");
            return "I'm sorry, I'm having a stroke.";
        }

        _logger.LogDebug("Chat completion successful: {Successful}", result.Successful);

        if (!result.Successful)
        {
            _logger.LogError(
                "Chat completion failed: {ErrorCode}:{ErrorMessage}",
                result.Error?.Code,
                result.Error?.Message
            );
            return "I'm sorry, I'm having a stroke.";
        }

        var choice =
            result.Choices.FirstOrDefault(x => x.FinishReason == "stop")
            ?? result.Choices.FirstOrDefault();

        if (choice is null)
        {
            _logger.LogWarning("Chat completion failed: {Error}", result.Error);
            return "I'm sorry, I don't know what to say.";
        }

        _logger.LogDebug("Chat completion finish reason: {FinishReason}", choice.FinishReason);

        return choice.Message.Content;
    }
}
