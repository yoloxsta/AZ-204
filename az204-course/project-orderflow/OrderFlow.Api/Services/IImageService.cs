namespace OrderFlow.Api.Services;

public interface IImageService
{
    Task<string> UploadProductImageAsync(string productId, Stream imageStream, string contentType);
    Task<string> GetProductImageUrlAsync(string productId);
}
