namespace ChampionsOfKhazad.Bot.Roster;

public interface IRosterStore
{
    Task<RosterState?> GetAsync(string id, CancellationToken cancellationToken);
    Task<bool> TrySaveAsync(RosterState state, CancellationToken cancellationToken);
}
