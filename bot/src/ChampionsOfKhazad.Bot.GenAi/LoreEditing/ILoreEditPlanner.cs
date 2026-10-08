using ChampionsOfKhazad.Bot.Lore.Abstractions;

namespace ChampionsOfKhazad.Bot.GenAi;

public interface ILoreEditPlanner
{
    Task<LoreEditPlan> PlanAsync(
        string instruction,
        IReadOnlyList<LoreEntrySnapshot> entries,
        IReadOnlyList<LoreEditorTurn> conversation,
        CancellationToken cancellationToken
    );
}
