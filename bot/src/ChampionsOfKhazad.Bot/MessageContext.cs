namespace ChampionsOfKhazad.Bot;

public record MessageContext(ulong UserId, IReadOnlyCollection<ulong> UserRoles);
