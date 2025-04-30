using System.Collections.Concurrent;
using System.ComponentModel;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.TextToImage;

namespace ChampionsOfKhazad.Bot.GenAi;

internal class ImageGenerationPlugin(
    GenAiImageGenerationConfig config,
    IGeneratedImageStore generatedImageStore,
    ITextToImageService textToImageService,
    ImageStorageService imageStorageService
)
{
    private static readonly ConcurrentDictionary<ulong, bool> UsersGeneratingImages = [];

    [KernelFunction]
    [Description("Generates an image based on the provided prompt.")]
    public async Task<GenerateImageResult> GenerateImageAsync(
        [Description("The text prompt describing the image to generate.")] string prompt,
        Kernel kernel,
        CancellationToken cancellationToken
    )
    {
        var userId = kernel.GetUserId();
        var userAllowance = config.DailyAllowances.GetValueOrDefault(userId, Constants.DefaultImageAllowance);

        switch (userAllowance)
        {
            case 0:
                return new GenerateImageResult(0, "User is not allowed to generate images.");
            case -1:
                return await GenerateImageAsync(prompt, userId, ushort.MaxValue, kernel, cancellationToken);
        }

        var generatedImageCount = await generatedImageStore.GetDailyGeneratedImageCountAsync(userId, cancellationToken);
        var remainingAllowance = userAllowance - generatedImageCount;

        if (remainingAllowance <= 0)
            return new GenerateImageResult(0, "User has reached their daily image generation limit.");

        var newRemainingAllowance = (ushort)(remainingAllowance - 1);

        try
        {
            if (!UsersGeneratingImages.TryAdd(userId, true))
                return new GenerateImageResult(newRemainingAllowance, "User is already generating an image.");

            return await GenerateImageAsync(prompt, userId, newRemainingAllowance, kernel, cancellationToken);
        }
        finally
        {
            UsersGeneratingImages.TryRemove(userId, out _);
        }
    }

    private async Task<GenerateImageResult> GenerateImageAsync(
        string prompt,
        ulong userId,
        ushort remainingAllowance,
        Kernel kernel,
        CancellationToken cancellationToken
    )
    {
        var timestamp = DateTime.UtcNow;
        var imageResponse = (await textToImageService.GetImageContentsAsync(prompt, kernel: kernel, cancellationToken: cancellationToken)).Single();
        var imageData = imageResponse.Data ?? throw new ApplicationException("Image data is null");

        var imageUri = await imageStorageService.UploadImageAsync($"{userId}-{timestamp:s}.{Constants.DefaultImageFileType}", imageData);
        var generatedImage = new GeneratedImage(prompt, userId, timestamp, imageUri);

        await generatedImageStore.SaveGeneratedImageAsync(generatedImage);

        return new GenerateImageResult(remainingAllowance, imageUri);
    }
}
