using System.Text.RegularExpressions;
using ChampionsOfKhazad.Bot.Lore;
using Microsoft.Extensions.Logging;
using OpenAI.Interfaces;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels.ResponseModels;

namespace ChampionsOfKhazad.Bot;

public class Assistant(IOpenAIService openAiService, ILogger<Assistant> logger, IGetRelatedLore relatedLoreGetter, BotContext context)
{
    private static readonly string Instructions = string.Join(
        '\n',
        "You are the Dwarf Lorekeeper of a \"World of Warcraft: Wrath of the Lich King\" guild known as Champions of Khazad.",
        "You know nothing about game content that was added after the expansion \"Wrath of the Lich King\".",
        "Your name is \"Lorekeeper Hammerstone\", users will also refer to you as \"CoK Bot\". Limit your replies to 100 words, and prefer shorter answers.",
        "You have a Dwarven accent."
    );

    private static readonly Regex EmojiExpression = new(@":(?<name>\w+):", RegexOptions.Compiled);

    private static readonly Dictionary<Role, string> RoleMap = new()
    {
        { Role.System, StaticValues.ChatMessageRoles.System },
        { Role.Assistant, StaticValues.ChatMessageRoles.Assistant },
        { Role.User, StaticValues.ChatMessageRoles.User },
    };

    public async Task<string> RespondAsync(
        string message,
        User user,
        IEnumerable<string> availableEmotes,
        IEnumerable<Message>? chatContext = null,
        Message? referencedMessage = null,
        string? instructions = null,
        string? model = null
    )
    {
        var relatedLore = await relatedLoreGetter.GetRelatedLoreAsync(message);

        const string separator = "\n\n###\n\n";

        var messages = (chatContext?.Select(x => new ChatMessage(RoleMap[x.Role], x.Content, x.Author?.Name)) ?? Array.Empty<ChatMessage>())
            .Prepend(
                ChatMessage.FromSystem(
                    string.Join(
                        separator,
                        string.Join(separator, relatedLore),
                        string.Join(
                            '\n',
                            instructions ?? Instructions,
                            $"You can use unicode emojis and the following guild emojis (do not invent extra emojis that are not in this list): {string.Join(", ", availableEmotes.Select(x => $":{x}:"))}"
                        )
                    )
                )
            )
            .Append(ChatMessage.FromUser(message, user.Name))
            .ToList();

        if (referencedMessage?.Author is not null)
        {
            messages.Add(
                ChatMessage.FromSystem(
                    $"The user is referencing this message from \"{referencedMessage.Author.Name}\": \"{referencedMessage.Content}\""
                )
            );
        }

        ChatCompletionCreateResponse result;

        try
        {
            result = await openAiService.ChatCompletion.CreateCompletion(
                new ChatCompletionCreateRequest
                {
                    Messages = messages,
                    Model = model ?? Constants.DefaultAssistantModel,
                    MaxTokens = 500,
                    N = 1,
                    User = user.Id.ToString(),
                }
            );
        }
        catch (Exception e)
        {
            logger.LogError(e, "Chat completion request failed");
            return "I'm sorry, I'm having a stroke.";
        }

        logger.LogDebug("Chat completion successful: {Successful}", result.Successful);

        if (!result.Successful)
        {
            logger.LogError("Chat completion failed: {ErrorCode}:{ErrorMessage}", result.Error?.Code, result.Error?.Message);
            return "I'm sorry, I'm having a stroke.";
        }

        var choice = result.Choices.FirstOrDefault(x => x.FinishReason == "stop") ?? result.Choices.FirstOrDefault();

        if (choice?.Message.Content is null)
        {
            logger.LogWarning("Chat completion failed: {@Error}", result.Error);
            return "I'm sorry, I don't know what to say.";
        }

        logger.LogDebug("Chat completion finish reason: {FinishReason}", choice.FinishReason);

        return ProcessEmojis(choice.Message.Content);
    }

    private string ProcessEmojis(string message)
    {
        var matches = EmojiExpression.Matches(message);

        foreach (Match match in matches)
        {
            var emojiName = match.Groups["name"].Value;

            if (context.Guild.Emotes.FirstOrDefault(x => string.Equals(x.Name, emojiName, StringComparison.InvariantCultureIgnoreCase)) is { } emoji)
                message = message.Replace(match.Value, emoji.ToString());
        }

        return message;
    }
}
