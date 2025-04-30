namespace ChampionsOfKhazad.Bot.GenAi;

internal record GenerateImageResult
{
    public ushort RemainingDailyAllowance { get; }
    public Uri? ImageUri { get; }
    public string? ErrorMessage { get; }
    public string Instructions =>
        "If the user's remaining daily allowance is less than 10, include it in your response. "
        + "If the image generation succeeds, include the image somewhere in your message with the syntax `[Text](URI)`. "
        + "Example: \"Here’s a depiction of myself, [Lorekeeper Hammerstone,](https://temporarystoage.blob.core.windows.net/generated-images/248801791955828746-2025-04-30T19%3A36%3A15.png) in the halls of Ironforge.\"";

    public GenerateImageResult(ushort remainingDailyAllowance, Uri imageUri)
        : this(remainingDailyAllowance, imageUri, null) { }

    public GenerateImageResult(ushort remainingDailyAllowance, string errorMessage)
        : this(remainingDailyAllowance, null, errorMessage) { }

    private GenerateImageResult(ushort remainingDailyAllowance, Uri? imageUri = null, string? errorMessage = null)
    {
        RemainingDailyAllowance = remainingDailyAllowance;
        ImageUri = imageUri;
        ErrorMessage = errorMessage;
    }
}
