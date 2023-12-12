namespace ChampionsOfKhazad.Bot;

public interface IFollowerResponseStrategy
{
    Task<string> GetResponseAsync(MessageReceived notification, CancellationToken cancellationToken = default);
}
