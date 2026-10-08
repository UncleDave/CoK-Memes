using ChampionsOfKhazad.Bot.Lore.Abstractions;

namespace ChampionsOfKhazad.Bot.Lore.Mongo;

internal record LoreDocument(string Name, string Content)
{
    public string? Pronouns { get; init; }
    public string? Nationality { get; init; }
    public string? MainCharacter { get; init; }
    public string? Biography { get; init; }
    public IReadOnlyList<string>? Aliases { get; init; }
    public IReadOnlyList<string>? Roles { get; init; }
    public float[]? Embedding { get; init; }
    public bool Deleted { get; init; }
    public string? Revision { get; init; }
    private IReadOnlyList<LoreRevision>? _history;
    public IReadOnlyList<LoreRevision> History
    {
        get => _history ?? [];
        init => _history = value;
    }

    public LoreDocument(IGuildLore guildLore)
        : this(guildLore.Name, guildLore.Content) { }

    public LoreDocument(IMemberLore memberLore)
        : this(memberLore.Name, memberLore.ToString() ?? string.Empty)
    {
        Pronouns = memberLore.Pronouns;
        Nationality = memberLore.Nationality;
        MainCharacter = memberLore.MainCharacter;
        Biography = memberLore.Biography;
        Aliases = memberLore.Aliases;
        Roles = memberLore.Roles;
    }

    public ILore ToModel() =>
        MainCharacter is not null
            ? new MemberLore(Name, Pronouns ?? "Unknown", Nationality ?? "Unknown", MainCharacter, Biography)
            {
                Aliases = Aliases ?? [],
                Roles = Roles ?? [],
            }
            : new GuildLore(Name, Content);

    public LoreEditState ToEditState() => new(Name, true, Revision, Deleted ? null : LoreEntrySnapshot.FromLore(ToModel()), History);

    public static LoreDocument FromSnapshot(LoreEntrySnapshot entry) =>
        entry.Kind switch
        {
            "guild" => new(new GuildLore(entry.Name, entry.Content)),
            "member" => new(
                new MemberLore(entry.Name, entry.Pronouns, entry.Nationality, entry.MainCharacter, entry.Biography)
                {
                    Aliases = entry.Aliases,
                    Roles = entry.Roles,
                }
            ),
            _ => throw new NotSupportedException("Unsupported lore kind."),
        };
}
