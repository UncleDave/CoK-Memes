using Discord;

namespace ChampionsOfKhazad.Bot.Portal;

public interface IDiscordClientProvider
{
    Task<IDiscordClient> GetClientAsync();
}
