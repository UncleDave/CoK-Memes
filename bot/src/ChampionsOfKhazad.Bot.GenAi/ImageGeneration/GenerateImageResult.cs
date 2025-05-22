namespace ChampionsOfKhazad.Bot.GenAi;

internal record GenerateImageResult
{
    public ushort RemainingUserDailyAllowance { get; }
    public Uri? ImageUri { get; }
    public string? ErrorMessage { get; }

    public GenerateImageResult(ushort remainingUserDailyAllowance, Uri imageUri)
        : this(remainingUserDailyAllowance, imageUri, null) { }

    public GenerateImageResult(ushort remainingUserDailyAllowance, string errorMessage)
        : this(remainingUserDailyAllowance, null, errorMessage) { }

    private GenerateImageResult(ushort remainingUserDailyAllowance, Uri? imageUri = null, string? errorMessage = null)
    {
        RemainingUserDailyAllowance = remainingUserDailyAllowance;
        ImageUri = imageUri;
        ErrorMessage = errorMessage;
    }
}
