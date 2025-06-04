using Discord;
using Discord.Rest;

namespace ChampionsOfKhazad.Bot.Portal;

public record DiscordClientProviderOptions(string BotToken);

public class DiscordClientProvider(DiscordRestClient discordClient, DiscordClientProviderOptions options)
{
    public async Task<IDiscordClient> GetClientAsync()
    {
        if (discordClient.LoginState == LoginState.LoggedIn)
        {
            return discordClient;
        }

        await discordClient.LoginAsync(TokenType.Bot, options.BotToken);

        return discordClient;
    }
}
