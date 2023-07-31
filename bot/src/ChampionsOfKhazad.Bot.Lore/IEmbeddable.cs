using ChampionsOfKhazad.Bot.OpenAi.Embeddings;

namespace ChampionsOfKhazad.Bot.Lore;

public interface IEmbeddable
{
    TextEntry ToTextEntry();
}
