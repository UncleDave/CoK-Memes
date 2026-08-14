using Microsoft.Extensions.AI;

namespace ChampionsOfKhazad.Bot.GenAi;

internal abstract class PersonalityBase(
    string personalityPrompt,
    bool includeLorekeeperTools,
    IEmojiHandler emojiHandler,
    IChatClient chatClient,
    PersonalityTools personalityTools
) : IPersonality
{
    private readonly string _systemPromptTemplate = string.Join(
        '\n',
        "## ROLE AND CONTEXT",
        personalityPrompt,
        "",
        "## AUTHOR INFORMATION",
        "You are responding to a Discord message from: {{$userName}}",
        "The author's identity and context are crucial for your response.",
        "",
        "## AVAILABLE RESOURCES",
        "### Guild Lore Lookup Policy:",
        "- Treat a direct question to this bot as potentially guild-local.",
        "- Call search_lore before answering any question that could refer to a guild member, player, character, name, nickname, alias, event, history, rule, or inside joke.",
        "- When a term is ambiguous or also has a well-known outside meaning, prefer its possible guild meaning and search first.",
        "- Do not guess, say you lack lore, or ask for more context until you have searched the relevant terms from the current message.",
        "- Skip search_lore only for requests that are clearly unrelated to guild lore.",
        "- You do not have public-web access. Do not claim to have searched or browsed the web, provide live citations, or present current external information as verified.",
        "",
        "### Available Emojis:",
        "Standard unicode emojis and these guild emojis are available for use:",
        "{{$emojis}}",
        "",
        "## RESPONSE GUIDELINES",
        "- Keep your response concise and under 100 words",
        "- Stay in character consistently",
        "- Reference the author ({{$userName}}) appropriately based on your role",
        "- Use emojis naturally when they enhance your message",
        "- Make your response engaging and contextually appropriate for Discord"
    );

    public virtual async Task<string> InvokeAsync(
        ChatHistory chatHistory,
        IMessageContext messageContext,
        CancellationToken cancellationToken = default
    )
    {
        var systemPrompt = _systemPromptTemplate
            .Replace("{{$userName}}", messageContext.UserName)
            .Replace("{{$emojis}}", string.Join(' ', emojiHandler.GetEmojis()))
            .Replace("{{$currentMonth}}", DateTimeOffset.Now.ToString("MMMM"));

        var messages = new ChatHistory([new ChatMessage(ChatRole.System, systemPrompt), .. chatHistory]);
        var options = new ChatOptions
        {
            Reasoning = new ReasoningOptions { Effort = ReasoningEffort.Medium },
            Tools = personalityTools.Create(messageContext, includeLorekeeperTools),
        };

        var response = await chatClient.GetResponseAsync(messages, options, cancellationToken);

        return emojiHandler.ProcessMessage(response.Text);
    }
}
