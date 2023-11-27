using Discord;
using Discord.WebSocket;

namespace ChampionsOfKhazad.Bot;

public class BotContext(ulong botId, IGuild guild, DiscordSocketClient client)
{
    public ulong BotId { get; } = botId;
    public IGuild Guild { get; } = guild;
    public DiscordSocketClient Client { get; } = client;
}
