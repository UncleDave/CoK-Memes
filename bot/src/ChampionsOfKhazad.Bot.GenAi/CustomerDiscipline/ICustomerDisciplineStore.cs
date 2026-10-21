namespace ChampionsOfKhazad.Bot.GenAi;

public interface ICustomerDisciplineStore
{
    Task<CustomerDisciplineState?> GetAsync(string id, CancellationToken cancellationToken);
    Task<bool> TrySaveAsync(CustomerDisciplineState state, CancellationToken cancellationToken);
}
