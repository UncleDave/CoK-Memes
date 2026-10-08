using ChampionsOfKhazad.Bot.Lore.Abstractions;

namespace ChampionsOfKhazad.Bot;

internal record LorePendingEdit(string Token, LoreEditState Expected, LoreEntrySnapshot? After, string Action, DateTimeOffset ExpiresAtUtc);
