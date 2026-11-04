using System.Text.Json;

namespace ChampionsOfKhazad.Bot.Roster.Blizzard;

public sealed record BlizzardDocument(JsonElement Data, DateTime ObservedAtUtc);
