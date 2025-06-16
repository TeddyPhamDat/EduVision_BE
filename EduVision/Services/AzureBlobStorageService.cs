using Azure.Storage.Blobs;
using System.IO;
using System.Threading.Tasks;

namespace EduVision.Services
{
    public class AzureBlobStorageService
    {
        private readonly string _connectionString;
        private readonly string _containerName;

        public string ConnectionString => _connectionString;
        public string ContainerName => _containerName;

        public AzureBlobStorageService(IConfiguration config)
        {
            _connectionString = config["AzureBlob:ConnectionString"] ?? throw new ArgumentNullException("Azure:BlobConnectionString is missing in configuration");
            _containerName = config["AzureBlob:Container"] ?? "presentations";
        }

        public async Task<string> UploadAsync(string blobName, Stream content, string contentType)
        {
            var blobServiceClient = new BlobServiceClient(_connectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(_containerName);
            await containerClient.CreateIfNotExistsAsync();
            var blobClient = containerClient.GetBlobClient(blobName);

            await blobClient.UploadAsync(content, overwrite: true);
            await blobClient.SetHttpHeadersAsync(new Azure.Storage.Blobs.Models.BlobHttpHeaders { ContentType = contentType });

            return blobClient.Uri.ToString();
        }
    }
}