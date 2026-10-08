namespace ChampionsOfKhazad.Bot.GenAi;

public record LoreEditPlan(string Action, string? Name, string? Kind, LoreEditChanges Changes, string Reply, bool RequiresConfirmation);
