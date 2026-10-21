namespace ChampionsOfKhazad.Bot.GenAi;

public interface IMessageContext
{
    ulong UserId { get; }
    string UserName { get; }
    ulong? ChannelId { get; }
    ulong? GuildId { get; }
    ulong? MessageId { get; }
    Task Reply(string message);
}
