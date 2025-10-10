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

    public ILore ToModel()
    {
        return MainCharacter is not null
            ? new global::ChampionsOfKhazad.Bot.Lore.MemberLore(Name, Pronouns!, Nationality!, MainCharacter, Biography)
            {
                Aliases = Aliases!,
                Roles = Roles!,
            }
            : new global::ChampionsOfKhazad.Bot.Lore.GuildLore(Name, Content);
    }
}
