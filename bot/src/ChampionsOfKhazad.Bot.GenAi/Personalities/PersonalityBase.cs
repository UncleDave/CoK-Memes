using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace ChampionsOfKhazad.Bot.GenAi;

internal abstract class PersonalityBase(
    string systemPrompt,
    Kernel kernel,
    IGetRelatedLore relatedLoreGetter,
    IEmojiHandler emojiHandler,
    IChatCompletionService chatCompletionService
) : IPersonality
{
    protected static readonly OpenAIPromptExecutionSettings DefaultPromptSettings = new()
    {
        ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
    };

    private static readonly IPromptTemplateFactory PromptTemplateFactory = new KernelPromptTemplateFactory();

    private readonly IPromptTemplate _systemPromptTemplate = PromptTemplateFactory.Create(
        new PromptTemplateConfig(
            string.Join(
                '\n',
                "## ROLE AND CONTEXT",
                systemPrompt,
                "",
                "## AUTHOR INFORMATION",
                "You are responding to a Discord message from: {{$userName}}",
                "The author's identity and context are crucial for your response.",
                "",
                "## AVAILABLE RESOURCES",
                "### Relevant Lore Entries:",
                "{{$lore}}",
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
            )
        )
    );

    public virtual async Task<string> InvokeAsync(
        ChatHistory chatHistory,
        IMessageContext messageContext,
        CancellationToken cancellationToken = default
    ) => await InvokeAsync(chatHistory, messageContext, new Dictionary<string, object?>(), cancellationToken);

    public virtual async Task<string> InvokeAsync(
        ChatHistory chatHistory,
        IMessageContext messageContext,
        IDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default
    )
    {
        kernel.SetMessageContext(messageContext);

        var input = chatHistory.Last();
        var lore = input.Content is not null ? await relatedLoreGetter.GetRelatedLoreAsync(input.Content) : [];

        var systemPrompt = await _systemPromptTemplate.RenderAsync(
            kernel,
            new KernelArguments(arguments)
            {
                { "userName", input.AuthorName },
                { "lore", string.Join("\n---\n\n", lore.Select(x => x.ToString())) },
                { "emojis", string.Join(' ', emojiHandler.GetEmojis()) },
            },
            cancellationToken
        );

        var chatHistoryWithSystemPrompt = new ChatHistory([new ChatMessageContent(AuthorRole.System, systemPrompt), .. chatHistory]);

        var response = await chatCompletionService.GetChatMessageContentAsync(
            chatHistoryWithSystemPrompt,
            DefaultPromptSettings,
            kernel,
            cancellationToken
        );

        return emojiHandler.ProcessMessage(response.ToString());
    }
}
