namespace ChampionsOfKhazad.Bot.GenAi;

public interface IPromptEnricher
{
    Task<string> EnrichPromptAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default);
}
