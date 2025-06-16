namespace EduVision.Services
{
    public class AzureBlobVideoStorage : IVideoStorageService
    {
        private readonly AzureBlobStorageService _blobStorage;
        private readonly ILogger<AzureBlobVideoStorage> _logger;

        public AzureBlobVideoStorage(
            AzureBlobStorageService blobStorage,
            ILogger<AzureBlobVideoStorage> logger)
        {
            _blobStorage = blobStorage;
            _logger = logger;
        }

        public async Task<string> UploadAsync(string localFilePath, string blobName, string contentType)
        {
            using var fs = File.OpenRead(localFilePath);
            _logger.LogInformation("Uploading video to: {BlobName}", blobName);
            return await _blobStorage.UploadAsync(blobName, fs, contentType);
        }
    }
}