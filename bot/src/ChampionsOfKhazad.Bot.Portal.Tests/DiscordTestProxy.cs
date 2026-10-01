using System.Reflection;

namespace ChampionsOfKhazad.Bot.Portal.Tests;

public class DiscordTestProxy : DispatchProxy
{
    public Func<MethodInfo, object?[]?, object?> InvokeMethod { get; set; } = null!;

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => InvokeMethod(targetMethod!, args);

    public static T Create<T>(Func<MethodInfo, object?[]?, object?> invoke)
        where T : class
    {
        var proxy = Create<T, DiscordTestProxy>();
        ((DiscordTestProxy)(object)proxy).InvokeMethod = invoke;
        return proxy;
    }
}
