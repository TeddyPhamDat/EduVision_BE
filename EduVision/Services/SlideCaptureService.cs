using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using EduVision.Models;

namespace EduVision.Services
{
    public class SlideCaptureService
    {
        private readonly AzureBlobStorageService _blobStorage;
        private readonly string _screenshotApiKey;

        public SlideCaptureService(
            AzureBlobStorageService blobStorage,
            IOptions<ScreenshotApiConfig> screenshotApiConfig)
        {
            _blobStorage = blobStorage;
            _screenshotApiKey = screenshotApiConfig.Value.ApiKey ?? throw new ArgumentNullException(nameof(screenshotApiConfig.Value.ApiKey));
        }

        public async Task<List<string>> CaptureSlidesAsync(string revealHtmlUrl, int slideCount, string lessonId)
        {
            using var httpClient = new HttpClient();

            var tasks = Enumerable.Range(0, slideCount).Select(async i =>
            {
                var slideUrl = $"{revealHtmlUrl}#/{i}";
                var apiUrl = $"https://shot.screenshotapi.net/screenshot?token={_screenshotApiKey}&url={Uri.EscapeDataString(slideUrl)}&output=image&file_type=png&wait_for_event=load";
                var imageBytes = await httpClient.GetByteArrayAsync(apiUrl);

                var blobName = $"presentations/{lessonId}/images/{i}.png";
                using var ms = new MemoryStream(imageBytes);
                return await _blobStorage.UploadAsync(blobName, ms, "image/png");
            });

            var imageUrls = await Task.WhenAll(tasks);
            return imageUrls.ToList();
        }
    }
}