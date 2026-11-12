namespace ChampionsOfKhazad.Bot.GenAi;

public interface ISpecialsLoreReader
{
    Task<SpecialsLoreBatch> ReadAsync(IReadOnlyList<SpecialsContext> contexts, CancellationToken cancellationToken);
    Task<bool> VerifyAsync(IReadOnlyList<SpecialsLoreReference> references, CancellationToken cancellationToken);
}
