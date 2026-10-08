namespace ChampionsOfKhazad.Bot.Lore.Abstractions;

public record LoreEditState(string Name, bool Exists, string? Revision, LoreEntrySnapshot? Entry, IReadOnlyList<LoreRevision> History);
