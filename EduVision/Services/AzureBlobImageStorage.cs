using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace EduVision.Services
{
    public class AzureBlobImageStorage : IImageStorageService
    {
        private readonly AzureBlobStorageService _blobStorage;

        public AzureBlobImageStorage(AzureBlobStorageService blobStorage)
        {
            _blobStorage = blobStorage;
        }

        public async Task<string> UploadImageAsync(Stream fileStream, string blobName, string contentType)
        {
            return await _blobStorage.UploadAsync(blobName, fileStream, contentType);
        }

        public async Task<List<string>> GetImagesByCategoryAsync(string category, int limit = 5)
        {
            var blobServiceClient = new BlobServiceClient(_blobStorage.ConnectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(_blobStorage.ContainerName);

            var imageUrls = new List<string>();
            await foreach (BlobItem blobItem in containerClient.GetBlobsAsync(prefix: category))
            {
                if (imageUrls.Count >= limit) break;
                var blobClient = containerClient.GetBlobClient(blobItem.Name);
                imageUrls.Add(blobClient.Uri.ToString());
            }
            return imageUrls;
        }
    }
}