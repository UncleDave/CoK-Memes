using System.Text.Json;
using ChampionsOfKhazad.Bot.Lore.Abstractions;

namespace ChampionsOfKhazad.Bot.GenAi;

internal sealed class SpecialsLoreReader(IGetLore lore, IGetRelatedLore relatedLore) : ISpecialsLoreReader
{
    internal const int MaximumEntryCharacters = 4000;
    internal const int MaximumSerializedCharacters = 12000;
    internal const int MaximumEntries = 12;

    public async Task<SpecialsLoreBatch> ReadAsync(IReadOnlyList<SpecialsContext> contexts, CancellationToken cancellationToken)
    {
        if (contexts.Count > 5)
            throw new SpecialsValidationException("LoreInputBounds");
        var loadedMembers = new Dictionary<ulong, IMemberLore?>();
        var references = new Dictionary<string, SpecialsLoreReference>(StringComparer.OrdinalIgnoreCase);
        var result = new List<SpecialsContext>();
        var characters = 100; // Reserve array/field separators around the complete attached references.
        var omitted = 0;
        foreach (var context in contexts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var anchors = context.Sources.Where(source => context.Event.SourceUrls.Contains(source.Url, StringComparer.Ordinal)).ToArray();
            var participants = anchors
                .Select(source => source.AuthorId)
                .Concat(anchors.SelectMany(source => source.MentionedUsers).Select(user => user.Id))
                .Concat(context.Sources.Select(source => source.AuthorId))
                .Distinct()
                .Take(3)
                .ToArray();
            var members = new List<ILore>();
            foreach (var id in participants)
            {
                if (!loadedMembers.TryGetValue(id, out var member))
                {
                    member = await lore.GetMemberLoreAsync(id, cancellationToken);
                    loadedMembers[id] = member;
                }
                if (member?.DiscordUserId == id && !string.IsNullOrWhiteSpace(member.Biography))
                    members.Add(member);
            }
            // A retrieval query is not evidence: use the selected summary and resolved names,
            // not a new broad chat scrape or unlinked member-name identity guesses.
            var query = context.Event.Summary + "\n" + string.Join("\n", anchors.Select(source => source.AuthorName).Distinct().Take(3));
            if (query.Length > 1000)
                throw new SpecialsValidationException("LoreQueryBounds");
            var related = await relatedLore.GetRelatedLoreAsync(query, 4).WaitAsync(cancellationToken);
            var guild = related.OfType<IGuildLore>().Take(4).Cast<ILore>().ToArray();
            var candidates = Enumerable
                .Range(0, Math.Max(guild.Length, members.Count))
                .SelectMany(index => new[] { index < guild.Length ? guild[index] : null, index < members.Count ? members[index] : null })
                .OfType<ILore>()
                .DistinctBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase);
            var selected = new List<SpecialsLoreReference>();
            foreach (var candidate in candidates)
            {
                if (selected.Count == 3)
                    break;
                var reference = CreateReference(
                    candidate,
                    references.TryGetValue(candidate.Name, out var existing) ? existing.Key : $"L{references.Count + 1}"
                );
                if (reference is null)
                    continue;
                reference = existing ?? reference;
                var size = JsonSerializer.Serialize(reference).Length;
                if (
                    reference.Content.Length > MaximumEntryCharacters
                    || characters + size > MaximumSerializedCharacters
                    || (existing is null && references.Count == MaximumEntries)
                )
                {
                    omitted++;
                    continue;
                }
                // Use one exact snapshot per name throughout a draft, even if a later lookup
                // races an edit. Used references are re-read before preview and approval.
                references[reference.Name] = reference;
                selected.Add(reference);
                characters += size;
            }
            result.Add(context with { Lore = selected });
        }
        return new(result, omitted);
    }

    public async Task<bool> VerifyAsync(IReadOnlyList<SpecialsLoreReference> references, CancellationToken cancellationToken)
    {
        if (references.Count > MaximumEntries)
            return false;
        foreach (var reference in references)
        {
            var current = await lore.GetLoreAsync(reference.Name, cancellationToken);
            if (current is null || CreateReference(current, reference.Key) != reference)
                return false;
        }
        return true;
    }

    private static SpecialsLoreReference? CreateReference(ILore entry, string key) =>
        entry switch
        {
            IGuildLore guild when !string.IsNullOrWhiteSpace(guild.Content) => new(key, guild.Name, guild.Content),
            IMemberLore { DiscordUserId: { } id, Biography: { } biography } member when !string.IsNullOrWhiteSpace(biography) => new(
                key,
                member.Name,
                biography,
                id
            ),
            _ => null,
        };
}
