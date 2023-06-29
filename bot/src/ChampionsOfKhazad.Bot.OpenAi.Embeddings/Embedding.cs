namespace ChampionsOfKhazad.Bot.OpenAi.Embeddings;

public record Embedding(string Id, string Text, float[] Vector) : TextEntry(Id, Text);
