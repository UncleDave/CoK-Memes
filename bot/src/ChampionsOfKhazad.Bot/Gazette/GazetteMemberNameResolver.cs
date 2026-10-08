using ChampionsOfKhazad.Bot.GenAi;

namespace ChampionsOfKhazad.Bot;

internal sealed class GazetteMemberNameResolver(Func<ulong, CancellationToken, Task<string?>> fetchName, Func<ulong, string?> cachedName)
{
    public async Task<IReadOnlyList<NotebookSource>> ResolveAsync(
        IReadOnlyList<NotebookSource> sources,
        bool refresh,
        CancellationToken cancellationToken
    )
    {
        // Resolve authors first; bound member requests independently of message/history bounds.
        var ids = sources
            .Select(source => source.AuthorId)
            .Concat(sources.SelectMany(source => source.MentionedUsers).Select(user => user.Id))
            .Distinct()
            .Take(64);
        var names = new Dictionary<ulong, string?>();
        foreach (var id in ids)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = refresh ? null : cachedName(id);
            name ??= await fetchName(id, cancellationToken);
            names[id] = string.IsNullOrWhiteSpace(name) ? null : name[..Math.Min(name.Length, 80)];
        }
        return sources
            .Select(source =>
            {
                var author = names.GetValueOrDefault(source.AuthorId) ?? "A guild member";
                var mentions = source.MentionedUsers.Select(user => new NotebookMentionedUser(user.Id, names.GetValueOrDefault(user.Id))).ToArray();
                return source with
                {
                    AuthorName = author,
                    MentionedUsers = mentions,
                    ViewHash = NotebookSource.CalculateViewHash(source.Content, author, mentions),
                };
            })
            .ToArray();
    }
}
