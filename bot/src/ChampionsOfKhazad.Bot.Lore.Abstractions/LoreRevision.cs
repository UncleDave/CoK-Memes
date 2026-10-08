namespace ChampionsOfKhazad.Bot.Lore.Abstractions;

public record LoreRevision(
    string Id,
    DateTime CreatedAtUtc,
    string Source,
    ulong? ActorId,
    string Action,
    LoreEntrySnapshot? Before,
    LoreEntrySnapshot? After
);
