using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using System.Security.Cryptography;

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
            // Fetch up to 100 images (adjust as needed)
            var result = await _cloudinary.Search()
                .Expression($"folder:{category}")
                .MaxResults(100)
                .ExecuteAsync();

            var allImages = result.Resources.Select(r => r.SecureUrl.ToString()).ToList();

            // Shuffle the list
            var rng = RandomNumberGenerator.Create();
            int n = allImages.Count;
            while (n > 1)
            {
                byte[] box = new byte[1];
                do rng.GetBytes(box);
                while (!(box[0] < n * (Byte.MaxValue / n)));
                int k = (box[0] % n);
                n--;
                (allImages[n], allImages[k]) = (allImages[k], allImages[n]);
            }

            // Take the first 'limit' images
            return allImages.Take(limit).ToList();
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
