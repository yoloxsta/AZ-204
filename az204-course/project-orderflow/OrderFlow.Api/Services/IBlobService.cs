using Azure.Storage.Blobs.Models;

namespace OrderFlow.Api.Services;

public interface IBlobService
{
    Task<string> UploadImageAsync(string blobName, Stream content, string contentType);
    Task<Stream> DownloadImageAsync(string blobName);
    Task<bool> DeleteImageAsync(string blobName);
    Task<List<string>> ListImagesAsync();
    Task<string> GetBlobSasUriAsync(string blobName);
}
