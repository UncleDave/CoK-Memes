namespace ChampionsOfKhazad.Bot.Roster;

public record RosterNoticeBatch(string Text, RosterNotice[] Notices, string? Title = null, uint? Colour = null);
