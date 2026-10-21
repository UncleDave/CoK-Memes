namespace ChampionsOfKhazad.Bot.GenAi;

public interface ICustomerDisciplineStore
{
    Task<CustomerDisciplineState?> GetAsync(string id, CancellationToken cancellationToken);
    Task<IReadOnlyList<CustomerDisciplineState>> ListAsync(
        ulong guildId,
        bool barredOnly,
        int offset,
        int limit,
        CancellationToken cancellationToken
    );
    Task<bool> TrySaveAsync(CustomerDisciplineState state, CancellationToken cancellationToken);
}
