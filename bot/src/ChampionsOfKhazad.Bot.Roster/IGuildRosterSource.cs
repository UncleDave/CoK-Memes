namespace ChampionsOfKhazad.Bot.Roster;

// This is our domain boundary, not a prediction of Blizzard's routes or response schema.
public interface IGuildRosterSource
{
    string Id { get; }
    bool IsMock { get; }
    bool HasSeparateProfessionRequests { get; }
    Task<RosterObservation> GetRosterAsync(CancellationToken cancellationToken);
    Task<ProfessionObservation?> GetProfessionsAsync(RosterCharacter character, CancellationToken cancellationToken);
}
