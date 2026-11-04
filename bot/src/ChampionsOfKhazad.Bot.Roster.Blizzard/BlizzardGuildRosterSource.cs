using System.Globalization;
using System.Net;
using System.Text.Json;

namespace ChampionsOfKhazad.Bot.Roster.Blizzard;

public sealed class BlizzardGuildRosterSource(BlizzardApiClient api, BlizzardRosterOptions options, TimeProvider clock) : IGuildRosterSource
{
    // Route changes are explicit source changes; do not carry mock/other-guild progress into live data.
    public string Id { get; } =
        $"blizzard-forever-v1:{options.Region}:{options.GuildSlug}:{options.RosterPathTemplate}:{options.ProfessionPathTemplate}";
    public bool IsMock => false;
    public bool HasSeparateProfessionRequests => true;
    private Dictionary<string, string> _profilePaths = new(StringComparer.Ordinal);

    public async Task<RosterObservation> GetRosterAsync(CancellationToken cancellationToken)
    {
        var path = options.RosterPathTemplate.Replace("{guild}", Uri.EscapeDataString(options.GuildSlug), StringComparison.Ordinal);
        var document = await api.GetAsync(path, options.ProfileNamespace, cancellationToken);
        try
        {
            var root = document.Data;
            var guild = root.GetProperty("guild");
            var name = Text(guild.GetProperty("name"));
            if (!string.Equals(name, options.GuildName, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Blizzard roster belongs to a different guild.");
            var members = root.GetProperty("members");
            if (
                members.ValueKind != JsonValueKind.Array
                || members.GetArrayLength() > 5000
                || HasNext(root)
                || (root.TryGetProperty("member_count", out var total) && total.GetInt32() != members.GetArrayLength())
            )
                throw new InvalidDataException("A complete, bounded Blizzard guild roster is required.");

            Dictionary<string, string>? classes = null;
            var characters = new List<RosterCharacter>();
            var profilePaths = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var member in members.EnumerateArray())
            {
                var character = member.GetProperty("character");
                var playableClass = character.GetProperty("playable_class");
                string className;
                if (playableClass.TryGetProperty("name", out var classText))
                    className = Text(classText);
                else
                {
                    classes ??= await ClassesAsync(cancellationToken);
                    if (!classes.TryGetValue(IdValue(playableClass.GetProperty("id")), out className!))
                        throw new InvalidDataException("Blizzard roster contains an unknown class ID.");
                }
                var item = new RosterCharacter
                {
                    Id = IdValue(character.GetProperty("id")),
                    FirstName = Text(character.GetProperty("name")),
                    LastName = Text(character.GetProperty("surname")),
                    ClassName = className,
                    Level = character.GetProperty("level").GetInt32(),
                };
                characters.Add(item);
                if (character.TryGetProperty("key", out var key) && key.TryGetProperty("href", out var href))
                    profilePaths[item.Id] = ProfilePath(Text(href));
            }
            var observation = RosterValidator.Validate(
                new RosterObservation
                {
                    GuildId = IdValue(guild.GetProperty("id")),
                    GuildName = name,
                    Complete = true,
                    ObservedAtUtc = document.ObservedAtUtc,
                    Characters = characters.ToArray(),
                },
                clock.GetUtcNow().UtcDateTime
            );
            _profilePaths = profilePaths;
            return observation;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (BlizzardApiException)
        {
            throw;
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new InvalidDataException(
                "Blizzard guild roster did not match the expected complete Forever schema; the existing roster was retained."
            );
        }
    }

    public async Task<ProfessionObservation?> GetProfessionsAsync(RosterCharacter character, CancellationToken cancellationToken)
    {
        var path =
            character.Id is not null && _profilePaths.TryGetValue(character.Id, out var profile)
                ? profile + "/professions"
                : options
                    .ProfessionPathTemplate.Replace("{first}", Uri.EscapeDataString(character.FirstName.ToLowerInvariant()), StringComparison.Ordinal)
                    .Replace("{last}", Uri.EscapeDataString(character.LastName.ToLowerInvariant()), StringComparison.Ordinal);
        BlizzardDocument document;
        try
        {
            document = await api.GetAsync(path, options.ProfileNamespace, cancellationToken);
        }
        catch (BlizzardApiException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        try
        {
            var root = document.Data;
            if (HasNext(root))
                throw new InvalidDataException("Paginated profession data is not a complete profession observation.");
            if (root.TryGetProperty("character", out var owner) && IdValue(owner.GetProperty("id")) != character.Id)
                throw new InvalidDataException("Blizzard profession data belongs to another character.");
            var complete = true;
            var sections = 0;
            var skills = new List<ProfessionSkill>();
            foreach (var section in new[] { "primaries", "secondaries" })
            {
                if (!root.TryGetProperty(section, out var entries))
                {
                    complete = false;
                    continue;
                }
                if (entries.ValueKind != JsonValueKind.Array)
                    throw new InvalidDataException("Invalid Blizzard profession section.");
                sections++;
                foreach (var entry in entries.EnumerateArray())
                {
                    var profession = entry.GetProperty("profession");
                    var id = IdValue(profession.GetProperty("id"));
                    var name = Text(profession.GetProperty("name"));
                    if (entry.TryGetProperty("tiers", out var tiers))
                        foreach (var tier in tiers.EnumerateArray())
                            Add(tier, IdValue(tier.GetProperty("tier").GetProperty("id")));
                    else
                        Add(entry, "");
                    void Add(JsonElement values, string tier) =>
                        skills.Add(
                            new ProfessionSkill
                            {
                                Id = id,
                                Name = name,
                                Tier = tier,
                                Skill = Rank(values, "skill_points"),
                                MaxSkill = Rank(values, "max_skill_points"),
                                ObservedAtUtc = document.ObservedAtUtc,
                            }
                        );
                }
            }
            if (sections == 0)
                return null;
            return RosterValidator.Validate(
                new ProfessionObservation
                {
                    Complete = complete,
                    ObservedAtUtc = document.ObservedAtUtc,
                    Skills = skills.ToArray(),
                },
                document.ObservedAtUtc,
                clock.GetUtcNow().UtcDateTime
            );
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new InvalidDataException("Blizzard professions did not match the expected schema; previous profession data was retained.");
        }
    }

    private async Task<Dictionary<string, string>> ClassesAsync(CancellationToken cancellationToken)
    {
        var document = await api.GetAsync("/data/wow/playable-class/index", options.StaticNamespace, cancellationToken);
        return document
            .Data.GetProperty("classes")
            .EnumerateArray()
            .ToDictionary(item => IdValue(item.GetProperty("id")), item => Text(item.GetProperty("name")), StringComparer.Ordinal);
    }

    private string ProfilePath(string href)
    {
        if (
            !Uri.TryCreate(href, UriKind.Absolute, out var uri)
            || uri.Scheme != "https"
            || uri.Host != options.ApiHost
            || !uri.IsDefaultPort
            || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Fragment)
            || !uri.AbsolutePath.StartsWith("/profile/wow/character/", StringComparison.Ordinal)
            || uri.AbsolutePath.EndsWith("/professions", StringComparison.Ordinal)
        )
            throw new InvalidDataException("Blizzard character link points outside the expected regional character resources.");
        // Ignore all returned query parameters; the client applies our own namespace and locale.
        return uri.AbsolutePath;
    }

    private static string IdValue(JsonElement value) =>
        value.ValueKind == JsonValueKind.Number && value.TryGetUInt64(out var id) && id > 0
            ? id.ToString(CultureInfo.InvariantCulture)
            : throw new InvalidDataException("Expected a positive stable Blizzard ID.");

    private static string Text(JsonElement value) =>
        value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!
            : throw new InvalidDataException("Expected Blizzard text data.");

    private static int? Rank(JsonElement value, string field) =>
        value.TryGetProperty(field, out var rank) && rank.ValueKind != JsonValueKind.Null ? rank.GetInt32() : null;

    private static bool HasNext(JsonElement root) =>
        root.TryGetProperty("_links", out var links) && links.TryGetProperty("next", out var next) && next.ValueKind != JsonValueKind.Null
        || root.TryGetProperty("page_count", out var pages) && pages.GetInt32() > 1;
}
