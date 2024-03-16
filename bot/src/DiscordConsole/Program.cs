using Discord;
using Discord.Rest;
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json").AddUserSecrets<Program>().Build();
var guildId = ulong.Parse(configuration["GuildId"]!);
var botToken = configuration["BotToken"]!;

var client = new DiscordRestClient();
await client.LoginAsync(TokenType.Bot, botToken);

var guild = await client.GetGuildAsync(guildId);

return;

async Task<RestMessage> GetMessage(RestGuild g, ulong channelId, ulong messageId)
{
    var channel = await g.GetTextChannelAsync(channelId);
    var message = await channel.GetMessageAsync(messageId);

    return message;
}
