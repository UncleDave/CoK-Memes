namespace ChampionsOfKhazad.Bot.GenAi;

public record CustomerDisciplineResult(bool Changed, CustomerDisciplineState? State, string? Error = null);
