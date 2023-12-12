namespace ChampionsOfKhazad.Bot;

public record Message(string Content, User? Author, Role Role = Role.User);
