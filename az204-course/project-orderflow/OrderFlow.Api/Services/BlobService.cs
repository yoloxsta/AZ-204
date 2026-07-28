using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Logging;

namespace OrderFlow.Api.Services;

public class BlobService : IBlobService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<BlobService> _logger;
    private const string ContainerName = "order-images";

    public BlobService(BlobServiceClient blobServiceClient, ILogger<BlobService> logger)
    {
        _blobServiceClient = blobServiceClient ?? throw new ArgumentNullException(nameof(blobServiceClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> UploadImageAsync(string blobName, Stream content, string contentType)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);
            var blobClient = containerClient.GetBlobClient(blobName);

            var headers = new BlobHttpHeaders { ContentType = contentType };
            var metadata = new Dictionary<string, string>
            {
                { "uploaded-at", DateTime.UtcNow.ToString("o") }
            };

            await blobClient.UploadAsync(content, new BlobUploadOptions
            {
                HttpHeaders = headers,
                Metadata = metadata
            });

            _logger.LogInformation("Blob {BlobName} uploaded", blobName);
            return blobClient.Uri.ToString();
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Failed to upload blob {BlobName}", blobName);
            throw;
        }
    }

    public async Task<Stream> DownloadImageAsync(string blobName)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
            var blobClient = containerClient.GetBlobClient(blobName);
            var response = await blobClient.DownloadStreamingAsync();
            return response.Value.Content;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            throw new FileNotFoundException($"Blob '{blobName}' not found.", ex);
        }
    }

    public async Task<bool> DeleteImageAsync(string blobName)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
            var blobClient = containerClient.GetBlobClient(blobName);
            var result = await blobClient.DeleteIfExistsAsync();
            return result.Value;
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Failed to delete blob {BlobName}", blobName);
            throw;
        }
    }

    public async Task<List<string>> ListImagesAsync()
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
            var blobNames = new List<string>();
            await foreach (var blobItem in containerClient.GetBlobsAsync())
            {
                blobNames.Add(blobItem.Name);
            }
            return blobNames;
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Failed to list blobs");
            throw;
        }
    }

    public async Task<string> GetBlobSasUriAsync(string blobName)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(ContainerName);
        var blobClient = containerClient.GetBlobClient(blobName);

        if (!await blobClient.ExistsAsync())
            throw new FileNotFoundException($"Blob '{blobName}' not found.");

        if (!blobClient.CanGenerateSasUri)
            throw new InvalidOperationException("Cannot generate SAS URI for this blob client.");

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = ContainerName,
            BlobName = blobName,
            Resource = "b",
            StartsOn = DateTimeOffset.UtcNow,
            ExpiresOn = DateTimeOffset.UtcNow.AddHours(1)
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        var sasUri = blobClient.GenerateSasUri(sasBuilder);
        return sasUri.ToString();
    }
}
