namespace ChampionsOfKhazad.Bot.Roster;

public interface IRosterNotificationSender
{
    Task<ulong> SendAsync(ulong guildId, ulong channelId, string text, CancellationToken cancellationToken);
}
