using Microsoft.Extensions.Logging;

namespace ChampionsOfKhazad.Bot.Tests;

internal sealed class RosterDiagnosticLogger<T> : ILogger<T>
{
    public List<Entry> Entries { get; } = [];

    public bool IsEnabled(LogLevel logLevel) => true;

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Entries.Add(new(logLevel, formatter(state, exception), exception, ((IEnumerable<KeyValuePair<string, object?>>)state!).ToDictionary()));

    internal sealed record Entry(LogLevel Level, string Message, Exception? Exception, Dictionary<string, object?> Properties);
}
