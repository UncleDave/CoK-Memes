namespace ChampionsOfKhazad.RosterManagement;

public interface IGetCharacters
{
    Task<IReadOnlyCollection<Character>> GetCharactersAsync(CancellationToken cancellationToken = default);
}
