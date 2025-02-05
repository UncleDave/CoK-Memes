using MediatR;

namespace ChampionsOfKhazad.Bot.DiscordMemes.StreakBreaks;

public record StreakBroken(ulong UserId, string EmoteName, DateTimeOffset Timestamp) : INotification;
