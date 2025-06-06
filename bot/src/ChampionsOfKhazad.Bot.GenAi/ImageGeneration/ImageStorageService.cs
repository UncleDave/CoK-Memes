using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace ChampionsOfKhazad.Bot.GenAi;

internal class ImageStorageService(BlobServiceClient blobServiceClient)
{
    public async Task UploadImageAsync(string imageName, ReadOnlyMemory<byte> data)
    {
        var containerClient = blobServiceClient.GetBlobContainerClient(Constants.GeneratedImagesBlobContainerName);
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

        var blobClient = containerClient.GetBlobClient(imageName);

        await blobClient.UploadAsync(
            BinaryData.FromBytes(data),
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = $"image/{Constants.DefaultImageFileType}" } }
        );
    }
}
