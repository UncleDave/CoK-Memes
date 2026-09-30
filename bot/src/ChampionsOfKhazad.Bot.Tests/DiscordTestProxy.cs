using System.Reflection;

namespace ChampionsOfKhazad.Bot.Tests;

public class DiscordTestProxy : DispatchProxy
{
    public Func<MethodInfo, object?[]?, object?> InvokeMethod { get; set; } = null!;

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => InvokeMethod(targetMethod!, args);
}
