using ChampionsOfKhazad.Bot.GenAi;
using Discord;

namespace ChampionsOfKhazad.Bot;

public class DiscordMessageContext(IUserMessage message) : IMessageContext
{
    public ulong UserId { get; } = message.Author.Id;

    public string UserName { get; } = message.GetAuthorName();

    public Task Reply(string content) => message.ReplyAsync(content);
}
