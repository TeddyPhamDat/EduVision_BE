using System.Text;
using System.IO;
using System.Threading.Tasks;
using EduVision.Models.DTO;
using EduVision.Services.Storage;

namespace EduVision.Services.Presentation
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

        public async Task<string> GenerateRevealHtmlAsync(List<LessonSlideDto> slides, string outputFilename, string templateName = "RevealTemplate.html")
        {
            var slideHtml = new StringBuilder();

            foreach (var slide in slides)
            {
                switch (templateName)
                {
                    case "RevealTemplateDark.html":
                        slideHtml.AppendLine("<section>");
                        if (!string.IsNullOrEmpty(slide.ImageUrl))
                            slideHtml.AppendLine($"<img class=\"slide-image\" src=\"{slide.ImageUrl}\" style=\"max-height: 320px; display:block; margin:auto;\"/>");
                        slideHtml.AppendLine($"<h2 class=\"slide-title\">{slide.Title}</h2>");
                        slideHtml.AppendLine($"<p class=\"slide-content\">{slide.Content}</p>");
                        slideHtml.AppendLine("</section>");
                        break;

                    case "RevealTemplateModern.html":
                        slideHtml.AppendLine("<section>");
                        slideHtml.AppendLine("<div class=\"slide-flex\">");
                        // Image panel (optional)
                        if (!string.IsNullOrEmpty(slide.ImageUrl))
                        {
                            slideHtml.AppendLine("<div class=\"slide-image-panel\">");
                            slideHtml.AppendLine($"<img class=\"slide-image\" src=\"{slide.ImageUrl}\" alt=\"Slide image\" />");
                            slideHtml.AppendLine("</div>");
                        }
                        // Content panel
                        slideHtml.AppendLine("<div class=\"slide-content-panel\">");
                        slideHtml.AppendLine($"<div class=\"slide-title\">{slide.Title}</div>");
                        slideHtml.AppendLine($"<div class=\"slide-content\">{slide.Content}</div>");
                        slideHtml.AppendLine("</div>");
                        slideHtml.AppendLine("</div>");
                        slideHtml.AppendLine("</section>");
                        break;


                    case "RevealTemplate.html":
                        slideHtml.AppendLine("<section>");
                        slideHtml.AppendLine("<div class=\"custom-card\">"); // Add this wrapper
                                                                             // Left panel: Title and Content
                        slideHtml.AppendLine("<div class=\"custom-left\">");
                        slideHtml.AppendLine($"<div class=\"custom-title\">{slide.Title}</div>");
                        slideHtml.AppendLine($"<div class=\"custom-content\">{slide.Content}</div>");
                        slideHtml.AppendLine("</div>");
                        // Only render right panel if image exists
                        if (!string.IsNullOrEmpty(slide.ImageUrl))
                        {
                            slideHtml.AppendLine("<div class=\"custom-right\">");
                            slideHtml.AppendLine("<div class=\"custom-image-frame\">");
                            slideHtml.AppendLine($"<img src=\"{slide.ImageUrl}\" alt=\"Slide image\" />");
                            slideHtml.AppendLine("</div>");
                            slideHtml.AppendLine("</div>"); // close custom-right
                        }
                        slideHtml.AppendLine("</div>"); // Close the custom-card wrapper
                        slideHtml.AppendLine("</section>");
                        break;

                    default: 
                        slideHtml.AppendLine("<section>");
                        if (!string.IsNullOrEmpty(slide.ImageUrl))
                            slideHtml.AppendLine($"<img class=\"slide-image\" src=\"{slide.ImageUrl}\" style=\"max-height: 320px; display:block; margin:auto;\"/>");
                        slideHtml.AppendLine($"<h2 class=\"slide-title\">{slide.Title}</h2>");
                        slideHtml.AppendLine($"<p class=\"slide-content\">{slide.Content}</p>");
                        slideHtml.AppendLine("</section>");
                        break;
                }
            }

            var templatePath = Path.Combine(_env.ContentRootPath, "Template", templateName);
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
