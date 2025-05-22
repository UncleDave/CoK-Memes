using Discord;
using Discord.WebSocket;

namespace ChampionsOfKhazad.Bot;

public record BotContext(ulong BotId, IGuild Guild, DiscordSocketClient Client);
