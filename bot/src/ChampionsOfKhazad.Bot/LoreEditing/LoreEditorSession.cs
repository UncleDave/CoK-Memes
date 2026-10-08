using ChampionsOfKhazad.Bot.GenAi;

namespace ChampionsOfKhazad.Bot;

// Only the configured administrator can use this process-local, short-lived conversation.
public sealed class LoreEditorSession
{
    internal SemaphoreSlim Gate { get; } = new(1, 1);
    internal List<LoreEditorTurn> Turns { get; } = [];
    internal LorePendingEdit? Pending { get; set; }
    internal DateTimeOffset LastUsedAtUtc { get; set; }
    internal string? LastEditedName { get; set; }
    internal string? LastEditedRevision { get; set; }

    internal void Reset()
    {
        Turns.Clear();
        Pending = null;
        LastEditedName = null;
        LastEditedRevision = null;
    }
}
