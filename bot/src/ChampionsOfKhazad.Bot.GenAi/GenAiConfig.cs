namespace ChampionsOfKhazad.Bot.GenAi;

public class GenAiConfig
{
    public string? OpenAiApiKey { get; set; }
    public string? GoogleSearchEngineId { get; set; }
    public string? GoogleSearchEngineApiKey { get; set; }
    public string? AzureStorageAccountName { get; set; }
    public string? AzureStorageAccountAccessKey { get; set; }
    public GenAiImageGenerationConfig ImageGeneration { get; } = new();
}

public class GenAiImageGenerationConfig
{
    public Dictionary<ulong, short> DailyAllowances { get; set; } = new();
}
