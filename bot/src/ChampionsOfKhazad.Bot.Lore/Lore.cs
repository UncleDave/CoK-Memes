using ChampionsOfKhazad.Bot.OpenAi.Embeddings;

namespace ChampionsOfKhazad.Bot.Lore;

public record Lore(string Name, string Content) : IEmbeddable
{
    public TextEntry ToTextEntry() => new(Name, Content);
}
