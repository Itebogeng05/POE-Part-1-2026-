using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace RaceDay.Api.Services;

/// <summary>Stores and retrieves uploaded files. The API is the only project that talks to Azure.</summary>
public interface IBlobStorageService
{
    /// <summary>Uploads a file and returns the generated blob name.</summary>
    Task<string> UploadAsync(Stream content, string contentType, string extension, CancellationToken ct = default);

    /// <summary>Downloads a file, or returns null when the blob does not exist.</summary>
    Task<BlobFile?> DownloadAsync(string blobName, CancellationToken ct = default);

    /// <summary>Deletes a file. Deleting a missing blob is not an error.</summary>
    Task DeleteAsync(string blobName, CancellationToken ct = default);
}

public sealed record BlobFile(Stream Content, string ContentType);

public class AzureBlobStorageService : IBlobStorageService
{
    private readonly BlobContainerClient? _container;
    private readonly ILogger<AzureBlobStorageService> _logger;
    private bool _containerReady;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public AzureBlobStorageService(IConfiguration config, ILogger<AzureBlobStorageService> logger)
    {
        _logger = logger;
        var connectionString = config["AzureStorage:ConnectionString"];
        var containerName = config["AzureStorage:ContainerName"] ?? "event-banners";

        if (!string.IsNullOrWhiteSpace(connectionString))
            _container = new BlobServiceClient(connectionString).GetBlobContainerClient(containerName);
    }

    private async Task<BlobContainerClient> GetContainerAsync(CancellationToken ct)
    {
        if (_container is null)
            throw new InvalidOperationException("Azure Blob Storage is not configured (AzureStorage:ConnectionString).");

        if (_containerReady) return _container;

        await _gate.WaitAsync(ct);
        try
        {
            if (!_containerReady)
            {
                // Private container: images are only served through the API, never directly from Azure.
                await _container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);
                _containerReady = true;
            }
        }
        finally
        {
            _gate.Release();
        }
        return _container;
    }

    public async Task<string> UploadAsync(Stream content, string contentType, string extension, CancellationToken ct = default)
    {
        var container = await GetContainerAsync(ct);
        var blobName = $"{Guid.NewGuid():N}{extension}";
        var blob = container.GetBlobClient(blobName);
        await blob.UploadAsync(content, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
        }, ct);
        return blobName;
    }

    public async Task<BlobFile?> DownloadAsync(string blobName, CancellationToken ct = default)
    {
        var container = await GetContainerAsync(ct);
        try
        {
            var response = await container.GetBlobClient(blobName).DownloadStreamingAsync(cancellationToken: ct);
            return new BlobFile(response.Value.Content, response.Value.Details.ContentType ?? "application/octet-stream");
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning("Blob {BlobName} was not found in storage.", blobName);
            return null;
        }
    }

    public async Task DeleteAsync(string blobName, CancellationToken ct = default)
    {
        var container = await GetContainerAsync(ct);
        await container.GetBlobClient(blobName).DeleteIfExistsAsync(cancellationToken: ct);
    }
}
