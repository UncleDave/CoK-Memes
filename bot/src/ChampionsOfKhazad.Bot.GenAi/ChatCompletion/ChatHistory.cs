using Microsoft.Extensions.AI;

namespace ChampionsOfKhazad.Bot.GenAi;

public class ChatHistory : List<ChatMessage>
{
    public ChatHistory() { }

    public ChatHistory(IEnumerable<ChatMessage> messages)
        : base(messages) { }

    public ChatHistory(string systemMessage)
        : this([new ChatMessage(ChatRole.System, systemMessage)]) { }
}
