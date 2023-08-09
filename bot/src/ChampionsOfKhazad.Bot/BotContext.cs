using Discord;
using Discord.WebSocket;

namespace ChampionsOfKhazad.Bot;

public class BotContext
{
    public ulong BotId { get; }
    public IGuild Guild { get; }
    public DiscordSocketClient Client { get; }

    public BotContext(ulong botId, IGuild guild, DiscordSocketClient client)
    {
        BotId = botId;
        Guild = guild;
        Client = client;
    }
}
