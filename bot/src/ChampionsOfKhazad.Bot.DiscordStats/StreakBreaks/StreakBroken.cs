using MediatR;

namespace ChampionsOfKhazad.Bot.DiscordStats.StreakBreaks;

public record StreakBroken(ulong UserId, string EmoteName, DateTimeOffset Timestamp) : INotification;
