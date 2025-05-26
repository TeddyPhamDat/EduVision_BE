using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;

namespace EduVision.Services
{
    public class CloudinaryImageService
    {
        private readonly Cloudinary _cloudinary;

        public CloudinaryImageService(IConfiguration config)
        {
            var account = new Account(
                config["Cloudinary:CloudName"],
                config["Cloudinary:ApiKey"],
                config["Cloudinary:ApiSecret"]
            );

            _cloudinary = new Cloudinary(account);
        }

        public async Task<List<string>> GetImagesByCategoryAsync(string category, int limit = 5)
        {
            var result = await _cloudinary.Search()
                .Expression($"folder:{category}")
                .MaxResults(limit)
                .ExecuteAsync();

            return result.Resources.Select(r => r.SecureUrl.ToString()).ToList();
        }

        public async Task<string> UploadImageAsync(IFormFile file, string category)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("File is missing.");

            await using var stream = file.OpenReadStream();

            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(file.FileName, stream),
                Folder = category // category becomes the folder name
            };

            var result = await _cloudinary.UploadAsync(uploadParams);

            if (result.StatusCode != System.Net.HttpStatusCode.OK)
                throw new Exception($"Upload failed: {result.Error?.Message}");

            return result.SecureUrl.ToString();
        }
    }
}
