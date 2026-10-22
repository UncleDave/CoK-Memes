using System.Text;

namespace ChampionsOfKhazad.Bot.Roster;

public record RosterCharacter
{
    public string? Id { get; init; }

    // Assigned by the refresh service only for name-only sources, for one continuous membership.
    public string? IdentityInstance { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required string ClassName { get; init; }
    public int Level { get; init; }
    public ProfessionObservation? Professions { get; init; }
    public DateTime? ProfessionsCheckedAtUtc { get; init; }
    public bool ProfessionRefreshFailed { get; init; }
    public string FullName => $"{FirstName} {LastName}";
    public string Key =>
        Id is not null ? $"id:{Id}"
        : IdentityInstance is not null ? $"name-instance:{IdentityInstance}"
        : LookupKey;
    public string LookupKey
    {
        get
        {
            if (Id is not null)
                return $"id:{Id}";
            var first = FirstName.Normalize(NormalizationForm.FormC).ToUpperInvariant();
            var last = LastName.Normalize(NormalizationForm.FormC).ToUpperInvariant();
            return $"name:{first.Length}:{first}{last}";
        }
    }
}
