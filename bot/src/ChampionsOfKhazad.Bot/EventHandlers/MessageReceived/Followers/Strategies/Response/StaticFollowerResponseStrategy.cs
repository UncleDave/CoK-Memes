namespace ChampionsOfKhazad.Bot;

public class StaticFollowerResponseStrategy(string response) : IFollowerResponseStrategy
{
    public Task<string> GetResponseAsync(MessageReceived notification, CancellationToken cancellationToken = default) => Task.FromResult(response);
}
