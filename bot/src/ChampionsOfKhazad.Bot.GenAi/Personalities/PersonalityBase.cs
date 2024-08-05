using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace ChampionsOfKhazad.Bot.GenAi;

internal abstract class PersonalityBase
{
    protected static readonly OpenAIPromptExecutionSettings DefaultPromptSettings =
        new() { ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions };
}
