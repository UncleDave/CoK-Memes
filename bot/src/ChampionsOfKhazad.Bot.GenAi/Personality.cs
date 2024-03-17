using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace ChampionsOfKhazad.Bot.GenAi;

internal abstract class Personality
{
    protected static readonly OpenAIPromptExecutionSettings DefaultPromptSettings =
        new() { ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions };
}
