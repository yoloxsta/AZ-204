using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace OrderFlow.Api.Services;

public class ImageService : IImageService
{
    private readonly IBlobService _blobService;
    private readonly ILogger<ImageService> _logger;
    private readonly string _imageContainerPath;

    public ImageService(IBlobService blobService, IConfiguration configuration, ILogger<ImageService> logger)
    {
        _blobService = blobService ?? throw new ArgumentNullException(nameof(blobService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _imageContainerPath = configuration["Storage:ImageContainerPath"] ?? "product-images";
    }

    public async Task<string> UploadProductImageAsync(string productId, Stream imageStream, string contentType)
    {
        try
        {
            var blobName = $"{_imageContainerPath}/{productId}/{Guid.NewGuid()}-{contentType.Replace("/", ".")}";

            var uri = await _blobService.UploadImageAsync(blobName, imageStream, contentType);
            _logger.LogInformation("Product image uploaded for product {ProductId}, blob: {BlobName}", productId, blobName);
            return uri;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload product image for product {ProductId}", productId);
            throw;
        }
    }

    public async Task<string> GetProductImageUrlAsync(string productId)
    {
        try
        {
            var blobName = $"{_imageContainerPath}/{productId}";
            var sasUri = await _blobService.GetBlobSasUriAsync(blobName);
            _logger.LogInformation("SAS URL generated for product image {ProductId}", productId);
            return sasUri;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate SAS URL for product {ProductId}", productId);
            throw;
        }
    }
}
