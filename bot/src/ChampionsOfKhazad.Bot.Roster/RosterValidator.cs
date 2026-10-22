namespace ChampionsOfKhazad.Bot.Roster;

public static class RosterValidator
{
    public static RosterObservation Validate(RosterObservation observation, DateTime now)
    {
        Text(observation.GuildName);
        if (observation.GuildId is not null)
            Text(observation.GuildId);
        Timestamp(observation.ObservedAtUtc, now);
        if (!observation.Complete || observation.Characters is null || observation.Characters.Length > 5000)
            throw new InvalidDataException("A complete bounded guild roster is required.");
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var character in observation.Characters)
        {
            if (character is null)
                throw new InvalidDataException("Invalid character.");
            Text(character.FirstName);
            Text(character.LastName);
            Text(character.ClassName);
            if (character.Id is not null)
                Text(character.Id);
            if (character.Level is < 1 or > 10000 || !keys.Add(character.LookupKey))
                throw new InvalidDataException("Invalid or duplicate character.");
        }
        return observation;
    }

    public static ProfessionObservation Validate(ProfessionObservation observation, DateTime fallback, DateTime now)
    {
        var timestamp = observation.ObservedAtUtc == default ? fallback : observation.ObservedAtUtc;
        Timestamp(timestamp, now);
        if (observation.Skills is null || observation.Skills.Length > 30)
            throw new InvalidDataException("Invalid profession observations.");
        var tracks = new HashSet<string>(StringComparer.Ordinal);
        var skills = observation
            .Skills.Select(skill =>
            {
                if (skill is null)
                    throw new InvalidDataException("Invalid profession.");
                Text(skill.Id);
                Text(skill.Name);
                if (skill.Tier is null || skill.Tier.Length > 100 || skill.Tier.Any(char.IsControl))
                    throw new InvalidDataException("Invalid profession tier.");
                if (
                    skill.Skill is < 0 or > 100000
                    || skill.MaxSkill is < 0 or > 100000
                    || (skill.Skill.HasValue && skill.MaxSkill.HasValue && skill.Skill > skill.MaxSkill)
                    || !tracks.Add(skill.TrackKey)
                )
                    throw new InvalidDataException("Invalid profession rank.");
                var observed = skill.ObservedAtUtc == default ? timestamp : skill.ObservedAtUtc;
                Timestamp(observed, now);
                return skill with { ObservedAtUtc = observed };
            })
            .ToArray();
        return observation with { ObservedAtUtc = timestamp, Skills = skills };
    }

    private static void Text(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 100 || value != value.Trim() || value.Any(char.IsControl))
            throw new InvalidDataException("Invalid roster text field.");
    }

    private static void Timestamp(DateTime value, DateTime now)
    {
        if (value.Kind != DateTimeKind.Utc || value == default || value > now.AddMinutes(1))
            throw new InvalidDataException("Invalid observation timestamp.");
    }
}
