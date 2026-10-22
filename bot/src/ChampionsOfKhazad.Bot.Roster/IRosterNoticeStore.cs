namespace ChampionsOfKhazad.Bot.Roster;

public interface IRosterNoticeStore
{
    Task AddPendingAsync(RosterNotice notice, CancellationToken cancellationToken);
    Task<IReadOnlyList<RosterNotice>> GetPendingAsync(string scopeId, CancellationToken cancellationToken);
    Task MarkSentAsync(string id, DateTime sentAtUtc, ulong messageId, CancellationToken cancellationToken);
    Task RemoveDepartedPendingAsync(string scopeId, string[] activeCharacterKeys, CancellationToken cancellationToken);
}
