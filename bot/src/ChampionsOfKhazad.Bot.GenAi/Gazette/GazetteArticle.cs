namespace ChampionsOfKhazad.Bot.GenAi;

public sealed record GazetteArticle(string Headline, string Body, IReadOnlyList<string> SourceUrls)
{
    public string? Teaser { get; init; }
}
