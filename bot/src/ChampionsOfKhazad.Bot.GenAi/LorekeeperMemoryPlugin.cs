using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace ChampionsOfKhazad.Bot.GenAi;

internal class LorekeeperMemoryPlugin(ILogger<LorekeeperMemoryPlugin> logger, LorekeeperMemory memory)
{
    [KernelFunction, Description("Save information to memory")]
    public async Task SaveAsync(
        [Description("The PascalCase key associated with the information to save")] string key,
        [Description("The information to save")] string input,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogDebug("Saving \"{Input}\" to memory with key \"{Key}\"", input, key);

        await memory.SaveAsync(key, input, cancellationToken: cancellationToken);
    }
}
