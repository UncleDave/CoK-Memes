using System.Collections.Concurrent;
using System.ComponentModel;
using JetBrains.Annotations;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.TextToImage;

namespace ChampionsOfKhazad.Bot.GenAi;

[UsedImplicitly]
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
    [UsedImplicitly]
    public async Task<GenerateImageResult> GenerateImageAsync(
        [Description("The text prompt describing the image to generate.")] string prompt,
        Kernel kernel,
        CancellationToken cancellationToken
    )
    {
        var messageContext = kernel.GetMessageContext();
        var userId = messageContext.UserId;
        var userAllowance = config.DailyAllowances.GetValueOrDefault(userId, Constants.DefaultImageAllowance);

        switch (userAllowance)
        {
            case 0:
                return new GenerateImageResult(0, "User is not allowed to generate images.");
            case -1:
                return await GenerateImageAsync(prompt, messageContext, ushort.MaxValue, kernel, cancellationToken);
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

            return await GenerateImageAsync(prompt, messageContext, newRemainingAllowance, kernel, cancellationToken);
        }
        finally
        {
            UsersGeneratingImages.TryRemove(userId, out _);
        }
    }

    private async Task<GenerateImageResult> GenerateImageAsync(
        string prompt,
        IMessageContext messageContext,
        ushort remainingAllowance,
        Kernel kernel,
        CancellationToken cancellationToken
    )
    {
        const string confirmationMessage = "Generating your image. This may take a minute.";

        await messageContext.Reply(
            remainingAllowance == ushort.MaxValue
                ? confirmationMessage
                : $"{confirmationMessage} After your image is generated, your remaining daily allowance will be {remainingAllowance}."
        );

        var userId = messageContext.UserId;
        var timestamp = DateTime.Now;
        var imageResponse = (await textToImageService.GetImageContentsAsync(prompt, kernel: kernel, cancellationToken: cancellationToken)).Single();
        var imageData = imageResponse.Data ?? throw new ApplicationException("Image data is null");

        var imageUri = await imageStorageService.UploadImageAsync($"{userId}-{timestamp:s}.{Constants.DefaultImageFileType}", imageData);
        var generatedImage = new GeneratedImage(prompt, userId, timestamp, imageUri);

        await generatedImageStore.SaveGeneratedImageAsync(generatedImage);

        return new GenerateImageResult(remainingAllowance, imageUri);
    }
}
