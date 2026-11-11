namespace ChampionsOfKhazad.Bot.GenAi;

public interface ISpecialsWriter
{
    Task<IReadOnlyList<SpecialsEvent>> DiscoverAsync(
        IReadOnlyList<NotebookSource> sources,
        DateTimeOffset until,
        CancellationToken cancellationToken
    );
    Task<IReadOnlyList<SpecialsEvent>> SelectAsync(
        IReadOnlyList<SpecialsEvent> candidates,
        DateTimeOffset until,
        CancellationToken cancellationToken
    );
    Task<IReadOnlyList<SpecialsDish>> WriteAsync(IReadOnlyList<SpecialsContext> events, DateTimeOffset until, CancellationToken cancellationToken);
}
