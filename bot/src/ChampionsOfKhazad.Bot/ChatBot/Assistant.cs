using System.Text.RegularExpressions;
using ChampionsOfKhazad.Bot.Lore;
using Microsoft.Extensions.Logging;
using OpenAI.Interfaces;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels.ResponseModels;

namespace ChampionsOfKhazad.Bot;

public class Assistant
{
    private static readonly string Instructions = string.Join(
        '\n',
        "You are the Dwarf Lorekeeper of a \"World of Warcraft: Wrath of the Lich King\" guild known as Champions of Khazad.",
        "You know nothing about game content that was added after the expansion \"Wrath of the Lich King\".",
        "Your name is \"Lorekeeper Hammerstone\", users will also refer to you as \"CoK Bot\". Limit your replies to 100 words, and prefer shorter answers.",
        "You have a Dwarven accent."
    );

    private static readonly Regex EmojiExpression = new(@":(?<name>\w+):", RegexOptions.Compiled);

    private readonly IOpenAIService _openAiService;
    private readonly ILogger<Assistant> _logger;
    private readonly IGetRelatedLore _relatedLoreGetter;
    private readonly BotContext _context;

    public Assistant(IOpenAIService openAiService, ILogger<Assistant> logger, IGetRelatedLore relatedLoreGetter, BotContext context)
    {
        _openAiService = openAiService;
        _logger = logger;
        _relatedLoreGetter = relatedLoreGetter;
        _context = context;
    }

    public async Task<string> RespondAsync(
        string message,
        User user,
        IEnumerable<string> availableEmotes,
        IEnumerable<ChatMessage>? chatContext = null,
        ChatMessage? referencedMessage = null,
        string? instructions = null,
        string? model = null
    )
    {
        var relatedLore = await _relatedLoreGetter.GetRelatedLoreAsync(message);

        const string separator = "\n\n###\n\n";

        var messages = (chatContext ?? Array.Empty<ChatMessage>())
            .Prepend(
                ChatMessage.FromSystem(
                    string.Join(
                        separator,
                        string.Join(separator, relatedLore),
                        string.Join(
                            '\n',
                            instructions ?? Instructions,
                            $"You can use unicode emojis and the following guild emojis: {string.Join(", ", availableEmotes.Select(x => $":{x}:"))}"
                        )
                    )
                )
            )
            .Append(ChatMessage.FromUser(message, user.Name))
            .ToList();

        if (referencedMessage is not null)
        {
            messages.Add(
                ChatMessage.FromSystem($"The user is referencing this message from \"{referencedMessage.Name}\": \"{referencedMessage.Content}\"")
            );
        }

        ChatCompletionCreateResponse result;

        try
        {
            result = await _openAiService.ChatCompletion.CreateCompletion(
                new ChatCompletionCreateRequest
                {
                    Messages = messages,
                    Model = model ?? Models.Gpt_4_1106_preview,
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
            _logger.LogError("Chat completion failed: {ErrorCode}:{ErrorMessage}", result.Error?.Code, result.Error?.Message);
            return "I'm sorry, I'm having a stroke.";
        }

        var choice = result.Choices.FirstOrDefault(x => x.FinishReason == "stop") ?? result.Choices.FirstOrDefault();

        if (choice is null)
        {
            _logger.LogWarning("Chat completion failed: {@Error}", result.Error);
            return "I'm sorry, I don't know what to say.";
        }

        _logger.LogDebug("Chat completion finish reason: {FinishReason}", choice.FinishReason);

        return ProcessEmojis(choice.Message.Content);
    }

    public async Task<string> RespondAsync(string instruction, string prompt, string? model = null)
    {
        var result = await _openAiService.ChatCompletion.CreateCompletion(
            new ChatCompletionCreateRequest
            {
                Messages = new[] { ChatMessage.FromSystem(instruction), ChatMessage.FromSystem(prompt) },
                Model = model ?? Models.Gpt_4_1106_preview,
                MaxTokens = 500,
                N = 1
            }
        );

        var choice = result.Choices.FirstOrDefault(x => x.FinishReason == "stop") ?? result.Choices.First();

        return ProcessEmojis(choice.Message.Content);
    }

    private string ProcessEmojis(string message)
    {
        var matches = EmojiExpression.Matches(message);

        foreach (Match match in matches)
        {
            var emojiName = match.Groups["name"].Value;

            if (_context.Guild.Emotes.FirstOrDefault(x => x.Name == emojiName) is { } emoji)
                message = message.Replace(match.Value, emoji.ToString());
        }

        return message;
    }
}
