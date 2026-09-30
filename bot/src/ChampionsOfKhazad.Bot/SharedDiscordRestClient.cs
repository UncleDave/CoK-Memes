using Discord;

namespace ChampionsOfKhazad.Bot;

/// <summary>
/// Non-owning access to DiscordSocketClient.Rest. Never register that disposable client
/// as a scoped service: disposing the REST wrapper also disposes the gateway's shared API client.
/// The singleton DiscordSocketClient owns the connection and its shutdown lifecycle.
/// </summary>
public sealed class SharedDiscordRestClient(IDiscordClient client)
{
    public IDiscordClient Client { get; } = client;
}
