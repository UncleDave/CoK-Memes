using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ChampionsOfKhazad.Bot.Roster;

// Re-read the fixture each time: editing it exercises progression and membership changes.
public class MockGuildRosterSource(string filename) : IGuildRosterSource
{
    public string Id { get; } = "mock-classic-v1:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(filename))));
    public bool IsMock => true;
    public bool HasSeparateProfessionRequests => true;

    public async Task<RosterObservation> GetRosterAsync(CancellationToken cancellationToken)
    {
        var timestamp = File.GetLastWriteTimeUtc(filename);
        await using var file = File.OpenRead(filename);
        using var document = await JsonDocument.ParseAsync(file, cancellationToken: cancellationToken);
        var root = document.RootElement;
        if (root.TryGetProperty("_mock_complete", out var complete) && !complete.GetBoolean())
            throw new InvalidDataException("Incomplete mock roster.");
        return new RosterObservation
        {
            GuildName = root.GetProperty("guild").GetProperty("name").GetString()!,
            GuildId = root.GetProperty("guild").GetProperty("id").GetUInt64().ToString(CultureInfo.InvariantCulture),
            ObservedAtUtc = timestamp,
            Complete = true,
            Characters = root.GetProperty("members")
                .EnumerateArray()
                .Select(member =>
                {
                    var character = member.GetProperty("character");
                    return new RosterCharacter
                    {
                        Id = CharacterId(character),
                        // These two fields are explicit Forever assumptions, not Classic fields.
                        FirstName = character.GetProperty("first_name").GetString()!,
                        LastName = character.GetProperty("last_name").GetString()!,
                        ClassName = character.GetProperty("playable_class").GetProperty("name").GetString()!,
                        Level = character.GetProperty("level").GetInt32(),
                    };
                })
                .ToArray(),
        };
    }

    public async Task<ProfessionObservation?> GetProfessionsAsync(RosterCharacter character, CancellationToken cancellationToken)
    {
        if (!ulong.TryParse(character.Id, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
            throw new InvalidDataException("A numeric mock character ID is required.");
        var path = Path.Combine(Path.GetDirectoryName(filename)!, "professions", $"{id.ToString(CultureInfo.InvariantCulture)}.json");
        var timestamp = File.GetLastWriteTimeUtc(path);
        await using var file = File.OpenRead(path);
        using var document = await JsonDocument.ParseAsync(file, cancellationToken: cancellationToken);
        var root = document.RootElement;
        if (CharacterId(root.GetProperty("character")) != character.Id)
            throw new InvalidDataException("Profession response belongs to a different character.");
        var skills = new List<ProfessionSkill>();
        var complete = true;
        foreach (var section in new[] { "primaries", "secondaries" })
        {
            if (!root.TryGetProperty(section, out var entries))
            {
                complete = false;
                continue;
            }
            foreach (var entry in entries.EnumerateArray())
            {
                var profession = entry.GetProperty("profession");
                var professionId = profession.GetProperty("id").GetInt32().ToString(CultureInfo.InvariantCulture);
                var name = profession.GetProperty("name").GetString()!;
                if (entry.TryGetProperty("tiers", out var tiers))
                {
                    foreach (var tier in tiers.EnumerateArray())
                        Add(tier, tier.GetProperty("tier").GetProperty("id").GetInt32().ToString(CultureInfo.InvariantCulture));
                }
                else
                    Add(entry, "");

                void Add(JsonElement values, string tier) =>
                    skills.Add(
                        new ProfessionSkill
                        {
                            Id = professionId,
                            Name = name,
                            Tier = tier,
                            Skill = values.TryGetProperty("skill_points", out var rank) ? rank.GetInt32() : null,
                            MaxSkill = values.TryGetProperty("max_skill_points", out var maximum) ? maximum.GetInt32() : null,
                            ObservedAtUtc = timestamp,
                        }
                    );
            }
        }
        return new ProfessionObservation
        {
            Complete = complete,
            ObservedAtUtc = timestamp,
            Skills = skills.ToArray(),
        };
    }

    private static string CharacterId(JsonElement character) => character.GetProperty("id").GetUInt64().ToString(CultureInfo.InvariantCulture);
}
