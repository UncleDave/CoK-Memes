using System.Text.Json;
using ChampionsOfKhazad.Bot.GenAi;

namespace ChampionsOfKhazad.Bot;

internal sealed record SpecialsDiscoveryPlan(IReadOnlyList<IReadOnlyList<NotebookSource>> Batches, int UnassessedMessages)
{
    internal const int MaximumBatches = 40;
    internal const int MaximumBatchCharacters = 32000;

    internal static SpecialsDiscoveryPlan Create(IReadOnlyList<NotebookSource> sources, DateTimeOffset since, DateTimeOffset until)
    {
        // Rotate channel/day batches so one busy channel cannot consume the entire discovery budget.
        var groups = sources
            .Where(source => source.TimestampUtc >= since.UtcDateTime && source.TimestampUtc <= until.UtcDateTime)
            .DistinctBy(source => source.Url)
            .GroupBy(source =>
                (Channel: source.Url[..source.Url.LastIndexOf('/')], Day: Math.Min(6, (int)((source.TimestampUtc - since.UtcDateTime).TotalDays)))
            )
            .OrderBy(group => group.Key.Day)
            .ThenBy(group => group.Key.Channel, StringComparer.Ordinal)
            .Select(group => new Queue<IReadOnlyList<NotebookSource>>(Split(group.OrderBy(source => source.TimestampUtc))))
            .ToArray();
        var batches = new List<IReadOnlyList<NotebookSource>>();
        while (batches.Count < MaximumBatches && groups.Any(group => group.Count > 0))
        {
            foreach (var group in groups)
            {
                if (batches.Count == MaximumBatches)
                    break;
                if (group.TryDequeue(out var batch))
                    batches.Add(batch);
            }
        }
        var assessed = batches.SelectMany(batch => batch).Select(source => source.Url).ToHashSet(StringComparer.Ordinal);
        return new(batches, sources.Select(source => source.Url).Distinct(StringComparer.Ordinal).Count(url => !assessed.Contains(url)));
    }

    private static IEnumerable<IReadOnlyList<NotebookSource>> Split(IEnumerable<NotebookSource> sources)
    {
        var batch = new List<NotebookSource>();
        var size = 100;
        foreach (var source in sources)
        {
            var length = JsonSerializer.Serialize(source).Length + 1;
            if (batch.Count > 0 && (size + length > MaximumBatchCharacters || batch.Count == 100))
            {
                yield return batch.ToArray();
                // Keep preceding context where it fits, without preventing forward progress.
                batch = batch.TakeLast(2).ToList();
                size = 100 + batch.Sum(item => JsonSerializer.Serialize(item).Length + 1);
                while (batch.Count > 0 && size + length > MaximumBatchCharacters)
                {
                    batch.RemoveAt(0);
                    size = 100 + batch.Sum(item => JsonSerializer.Serialize(item).Length + 1);
                }
            }
            if (length + 100 > MaximumBatchCharacters)
                continue;
            batch.Add(source);
            size += length;
        }
        if (batch.Count > 0)
            yield return batch.ToArray();
    }

    internal static IReadOnlyList<SpecialsContext> AddContext(IReadOnlyList<SpecialsEvent> events, IReadOnlyList<NotebookSource> sources)
    {
        var byUrl = sources.ToDictionary(source => source.Url, StringComparer.Ordinal);
        var channels = sources
            .GroupBy(source => source.Url[..source.Url.LastIndexOf('/')])
            .ToDictionary(group => group.Key, group => group.OrderBy(source => source.TimestampUtc).ToArray(), StringComparer.Ordinal);
        return events
            .Select(item =>
            {
                if (item.SourceUrls.Count is < 1 or > 3 || item.SourceUrls.Any(url => !byUrl.ContainsKey(url)))
                    throw new SpecialsValidationException("SelectedEvidence");
                var anchors = item.SourceUrls.Select(url => byUrl[url]).ToArray();
                var context = new List<NotebookSource>(anchors);
                foreach (var anchor in anchors)
                {
                    var channel = channels[anchor.Url[..anchor.Url.LastIndexOf('/')]];
                    var index = Array.FindIndex(channel, source => source.Url == anchor.Url);
                    context.AddRange(
                        channel[Math.Max(0, index - 3)..Math.Min(channel.Length, index + 4)]
                            .Where(source => Math.Abs((source.TimestampUtc - anchor.TimestampUtc).TotalMinutes) <= 30)
                    );
                }
                // Always preserve complete anchors; then closest neighbouring messages within a serialized input budget.
                var selected = anchors.DistinctBy(source => source.Url).ToList();
                var size = selected.Sum(source => JsonSerializer.Serialize(source).Length);
                foreach (
                    var source in context
                        .DistinctBy(source => source.Url)
                        .Where(source => selected.All(existing => existing.Url != source.Url))
                        .OrderBy(source => anchors.Min(anchor => Math.Abs((source.TimestampUtc - anchor.TimestampUtc).Ticks)))
                )
                {
                    var length = JsonSerializer.Serialize(source).Length;
                    if (selected.Count < 12 && size + length <= 15000)
                    {
                        selected.Add(source);
                        size += length;
                    }
                }
                return new SpecialsContext(item, selected.OrderBy(source => source.TimestampUtc).ToArray());
            })
            .ToArray();
    }
}
