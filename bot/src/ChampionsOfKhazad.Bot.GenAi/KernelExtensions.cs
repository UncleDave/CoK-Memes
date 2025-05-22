using Microsoft.SemanticKernel;

namespace ChampionsOfKhazad.Bot.GenAi;

internal static class KernelExtensions
{
    public static void SetMessageContext(this Kernel kernel, IMessageContext messageContext) =>
        kernel.Data[Constants.KernelDataMessageContextKey] = messageContext;

    public static IMessageContext GetMessageContext(this Kernel kernel)
    {
        if (kernel.Data.TryGetValue(Constants.KernelDataMessageContextKey, out var messageContext))
        {
            return messageContext as IMessageContext
                ?? throw new InvalidOperationException("Message context is set in kernel but is the wrong type.");
        }

        throw new InvalidOperationException("Message context not set in the kernel.");
    }
}
