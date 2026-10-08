namespace ChampionsOfKhazad.Bot.GenAi;

public sealed record GazetteEdition(IReadOnlyList<GazetteArticle> Articles, string Editorial);
