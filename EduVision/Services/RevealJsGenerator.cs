using EduVision.Models;
using System.Text;
using System.IO;
using System.Threading.Tasks;

namespace EduVision.Services
{
    public class RevealJsGenerator
    {
        private readonly IWebHostEnvironment _env;
        private readonly AzureBlobStorageService _blobStorage;

        public RevealJsGenerator(IWebHostEnvironment env, AzureBlobStorageService blobStorage)
        {
            _env = env;
            _blobStorage = blobStorage;
        }

        public async Task<string> GenerateRevealHtmlAsync(List<LessonSlide> slides, string outputFilename)
        {
            var slideHtml = new StringBuilder();

            foreach (var slide in slides)
            {
                slideHtml.AppendLine("<section>");
                if (!string.IsNullOrEmpty(slide.ImageUrl))
                {
                    slideHtml.AppendLine($"<img src=\"{slide.ImageUrl}\" style=\"max-height: 300px; display:block; margin:auto;\"/>");
                }
                slideHtml.AppendLine($"<h2>{slide.Title}</h2>");
                slideHtml.AppendLine($"<p>{slide.Content}</p>");
                slideHtml.AppendLine("</section>");
            }

            var templatePath = Path.Combine(_env.ContentRootPath, "Testing", "RevealTemplate.html");
            var template = await File.ReadAllTextAsync(templatePath);

            var fullHtml = template.Replace("{{slides}}", slideHtml.ToString());

            // Upload to Azure Blob Storage
            var blobName = $"presentations/{outputFilename}.html";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(fullHtml));
            var url = await _blobStorage.UploadAsync(blobName, stream, "text/html");

            return url;
        }
    }
}
