namespace ChampionsOfKhazad.Bot.Roster;

// An expected absence of the configured roster, not an empty membership response.
public sealed class RosterSourceUnavailableException(string message) : Exception(message);
