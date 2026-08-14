using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using OpenAI.Images;

namespace ChampionsOfKhazad.Bot.GenAi;

internal class ImageGenerationService(
    GenAiImageGenerationConfig config,
    IGeneratedImageStore generatedImageStore,
    ImageClient imageClient,
    ImageStorageService imageStorageService,
    ILogger<ImageGenerationService> logger
)
{
    private static readonly ConcurrentDictionary<ulong, byte> UsersGeneratingImages = [];

    public async Task<GenerateImageResult> GenerateImageAsync(string prompt, IMessageContext messageContext, CancellationToken cancellationToken)
    {
        var userId = messageContext.UserId;
        var remainingAllowance = (ushort)0;
        var stage = "checking the daily allowance";
        var ownsGenerationLock = false;

        logger.LogInformation("Image generation requested by Discord user {UserId} with a {PromptLength}-character prompt.", userId, prompt.Length);

        try
        {
            var userAllowance = config.DailyAllowances.GetValueOrDefault(userId, Constants.DefaultImageAllowance);

            if (userAllowance == 0)
            {
                logger.LogInformation("Image generation denied for Discord user {UserId}: image generation is not allowed.", userId);
                return new GenerateImageResult(0, "User is not allowed to generate images.");
            }

            var availableAllowance =
                userAllowance == -1 ? ushort.MaxValue : await GetAvailableAllowanceAsync(userId, userAllowance, cancellationToken);

            if (availableAllowance == 0)
            {
                logger.LogInformation("Image generation denied for Discord user {UserId}: daily allowance exhausted.", userId);
                return new GenerateImageResult(0, "User has reached their daily image generation limit.");
            }

            remainingAllowance = availableAllowance == ushort.MaxValue ? ushort.MaxValue : (ushort)(availableAllowance - 1);

            if (!UsersGeneratingImages.TryAdd(userId, 0))
            {
                logger.LogInformation("Image generation denied for Discord user {UserId}: another request is already in progress.", userId);
                return new GenerateImageResult(remainingAllowance, "User is already generating an image.");
            }

            ownsGenerationLock = true;
            stage = "sending the confirmation reply";
            await messageContext.Reply(
                remainingAllowance == ushort.MaxValue
                    ? Constants.ImageGenerationConfirmationMessage
                    : $"{Constants.ImageGenerationConfirmationMessage} After your image is generated, your remaining daily allowance will be {remainingAllowance}."
            );

            var timestamp = DateTimeOffset.Now;
            stage = "requesting an image from OpenAI";
            var image = await imageClient.GenerateImageAsync(prompt, new ImageGenerationOptions(), cancellationToken);
            stage = "reading the OpenAI image response";
            var imageData = image.Value.ImageBytes ?? throw new ApplicationException("OpenAI returned no image data.");
            var imageName = $"{userId}-{timestamp.ToUniversalTime():yyyyMMddTHHmmssfffffffZ}-{Guid.NewGuid():N}.{Constants.DefaultImageFileType}";

            stage = "uploading the image to blob storage";
            await imageStorageService.UploadImageAsync(imageName, imageData.ToArray(), cancellationToken);

            stage = "saving the generated-image record";
            await generatedImageStore.SaveGeneratedImageAsync(new GeneratedImage(prompt, userId, timestamp, imageName));

            logger.LogInformation("Image generation completed for Discord user {UserId}.", userId);
            return new GenerateImageResult(remainingAllowance, new Uri($"{Constants.GeneratedImagesBaseUrl}/{imageName}"));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Image generation cancelled while {Stage} for Discord user {UserId}.", stage, userId);
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Image generation failed while {Stage} for Discord user {UserId}.", stage, userId);
            return new GenerateImageResult(remainingAllowance, "Image generation failed. Please try again later.");
        }
        finally
        {
            if (ownsGenerationLock)
                UsersGeneratingImages.TryRemove(userId, out _);
        }
    }

    private async Task<ushort> GetAvailableAllowanceAsync(ulong userId, short userAllowance, CancellationToken cancellationToken)
    {
        var generatedImageCount = await generatedImageStore.GetDailyGeneratedImageCountAsync(userId, cancellationToken);
        var availableAllowance = userAllowance - generatedImageCount;

        return availableAllowance > 0 ? (ushort)availableAllowance : (ushort)0;
    }

    public async Task<string> SearchGeneratedImagesAsync(
        string searchText,
        bool onlyMine,
        IMessageContext messageContext,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation(
            "Generated image search requested by Discord user {UserId}. Search is restricted to the requesting user: {OnlyMine}.",
            messageContext.UserId,
            onlyMine
        );

        try
        {
            var userId = onlyMine ? messageContext.UserId : (ulong?)null;
            var images = await generatedImageStore.GetAsync(take: 4, userId: userId, searchText: searchText, cancellationToken: cancellationToken);

            if (images.Count == 0)
                return "No images found matching the search query.";

            var results = images.Select(image => $"- {image.Prompt}: {Constants.GeneratedImagesBaseUrl}/{image.Filename}").ToArray();

            return $"Found {images.Count} image(s):\n{string.Join("\n", results)}";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Generated image search was cancelled for Discord user {UserId}.", messageContext.UserId);
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Generated image search failed for Discord user {UserId}.", messageContext.UserId);
            return "Generated image search failed. Tell the user that image search is temporarily unavailable.";
        }
    }
}
