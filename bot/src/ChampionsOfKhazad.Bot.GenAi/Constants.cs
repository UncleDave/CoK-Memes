namespace ChampionsOfKhazad.Bot.GenAi;

public static class Constants
{
    public const string LorekeeperName = "Lorekeeper Hammerstone";
    public const string OpenAiFriendlyLorekeeperName = "Hammerstone";
    public const string ImageGenerationConfirmationMessage = "Generating your image. This may take a minute.";

    internal const string DefaultCompletionsModel = "gpt-4.1";
    internal const string DefaultImageModel = "gpt-image-1";
    internal const string DefaultEmbeddingModel = "text-embedding-3-small";

    internal const string GeneratedImagesBlobContainerName = "generated-images";
    public const string GeneratedImagesBaseUrl = $"https://images.championsofkhazad.com/{GeneratedImagesBlobContainerName}";

    internal const string DefaultImageFileType = "png";
    internal const short DefaultImageAllowance = 2;
}
