using EduVision.DBContext;
using EduVision.Models;
using EduVision.Models.DTO.Request;
using EduVision.Services.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduVision.Controllers
{
    // This controller manages image storage and retrieval using Azure Blob Storage.
    // It replaces any Cloudinary-based logic, ensuring all image operations are centralized and abstracted.
    // API convention: Use plural, hyphenated nouns for resource URIs (e.g., /api/blob-images).
    [Authorize(Roles = "MANAGER")]
    [ApiController]
    [Route("api/blob-images")]
    public class BlobImageController : ControllerBase
    {
        private readonly IImageStorageService _imageStorage;
        private readonly EduVisionContext _dbContext;

        // Constructor injects the image storage abstraction and database context.
        // Why: Follows SOLID principles and allows for easy backend replacement.
        public BlobImageController(IImageStorageService imageStorage, EduVisionContext dbContext)
        {
            _imageStorage = imageStorage;
            _dbContext = dbContext;
        }

        /// <summary>
        /// Uploads an image to Azure Blob Storage and records its metadata in the database.
        /// </summary>
        // Why: Centralizes image upload and metadata management for all image resources.
        [HttpPost("images")]
        public async Task<IActionResult> CreateImage(
            IFormFile file,
            [FromForm] ImageUploadRequestDto request)
        {
            if (file == null)
                return BadRequest("File is required.");

            if (string.IsNullOrWhiteSpace(request.Category))
                return BadRequest("Category is required.");

            // Enforce that if any metadata is provided, all must be present.
            bool anyMetadata =
                (request.Grade.HasValue && request.Grade.Value != 0) ||
                !string.IsNullOrWhiteSpace(request.Chapter);

            bool allMetadata =
                (request.Grade.HasValue && request.Grade.Value != 0) &&
                !string.IsNullOrWhiteSpace(request.Chapter);

            if (anyMetadata && !allMetadata)
                return BadRequest("If you provide either Grade or Chapter, you must provide both.");

            // Organize images in blob storage by category/grade/chapter for efficient retrieval.
            string folder = allMetadata
                ? $"{request.Category}/Grade{request.Grade}/{request.Chapter}"
                : $"{request.Category}/Default";

            string blobName = $"{folder}/{Guid.NewGuid()}_{file.FileName}";
            string url = await _imageStorage.UploadImageAsync(file.OpenReadStream(), blobName, file.ContentType);

            // Store image metadata in the database for fast queries and filtering.
            var image = new Image
            {
                Url = url,
                Status = "Active",
                Category = request.Category,
                Grade = allMetadata ? $"Grade{request.Grade}" : "None",
                Chapter = allMetadata ? request.Chapter! : "None"
            };

            _dbContext.Images.Add(image);
            await _dbContext.SaveChangesAsync();

            return Ok(new { imageUrl = url, imageId = image.ImageId });
        }

        /// <summary>
        /// Retrieves images from the database, filtered by category, grade, and chapter.
        /// </summary>
        // Why: Allows managers to search and filter images for reuse or review.
        [HttpGet("images")]
        public async Task<IActionResult> GetImages(
            [FromQuery] string category,
            [FromQuery] string grade,
            [FromQuery] string chapter,
            [FromQuery] int limit = 20)
        {
            // Query images by metadata for flexible filtering.
            var query = _dbContext.Images.AsQueryable();

            if (!string.IsNullOrWhiteSpace(category))
                query = query.Where(i => i.Category == category);

            if (!string.IsNullOrWhiteSpace(grade))
                query = query.Where(i => i.Grade == grade);
            else
                query = query.Where(i => i.Grade == "None");

            if (!string.IsNullOrWhiteSpace(chapter))
                query = query.Where(i => i.Chapter == chapter);
            else
                query = query.Where(i => i.Chapter == "None");

            var images = await query
                .OrderByDescending(i => i.ImageId)
                .Take(limit)
                .Select(i => new
                {
                    i.ImageId,
                    i.Url,
                    i.Status,
                    i.Category,
                    i.Grade,
                    i.Chapter
                })
                .ToListAsync();

            return Ok(images);
        }
    }
}