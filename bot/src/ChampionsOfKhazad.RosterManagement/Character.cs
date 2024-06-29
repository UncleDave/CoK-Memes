namespace ChampionsOfKhazad.RosterManagement;

public class Character
{
    public Guid Id { get; }
    public ulong GuildMemberDiscordId { get; }
    public string Name { get; }
    public Spec Spec { get; }

    internal Character(Guid id, ulong guildMemberDiscordId, string name, Spec spec)
    {
        Id = id;
        GuildMemberDiscordId = guildMemberDiscordId;
        Name = name;
        Spec = spec;
    }

    public Character(ulong guildMemberDiscordId, string name, Spec spec)
        : this(Guid.NewGuid(), guildMemberDiscordId, name, spec) { }
}
