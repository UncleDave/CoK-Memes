using Microsoft.SemanticKernel;

namespace ChampionsOfKhazad.Bot.GenAi;

internal static class KernelExtensions
{
    public static void SetUserId(this Kernel kernel, ulong userId) => kernel.Data[Constants.KernelDataUserIdKey] = userId;

    public static ulong GetUserId(this Kernel kernel)
    {
        if (kernel.Data.TryGetValue(Constants.KernelDataUserIdKey, out var userId))
        {
            return userId is ulong uid ? uid : throw new InvalidOperationException("User ID is set in kernel but is the wrong type.");
        }

        throw new InvalidOperationException("User ID not set in the kernel.");
    }
}
