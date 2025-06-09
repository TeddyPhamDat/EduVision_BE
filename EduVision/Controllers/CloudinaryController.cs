using EduVision.DBContext;
using EduVision.Models;
using EduVision.Models.DTO.Request;
using EduVision.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduVision.Controllers
{
    [Authorize(Roles = "MANAGER")]
    [ApiController]
    [Route("api/[controller]")]
    public class CloudinaryController : ControllerBase
    {
        private readonly CloudinaryImageService _cloudinary;
        private readonly EduVisionContext _dbContext;

        public CloudinaryController(CloudinaryImageService cloudinary, EduVisionContext dbContext)
        {
            _cloudinary = cloudinary;
            _dbContext = dbContext;
        }

        [HttpPost("upload-image")]
        public async Task<IActionResult> UploadImage(
            IFormFile file,
            [FromForm] ImageUploadRequestDto request)
        {
            if (file == null)
                return BadRequest("File is required.");

            if (string.IsNullOrWhiteSpace(request.Category))
                return BadRequest("Category is required.");

            // If any metadata (except Category) is provided, require all of them
            bool anyMetadata =
                (request.Grade.HasValue && request.Grade.Value != 0) ||
                !string.IsNullOrWhiteSpace(request.Chapter);

            bool allMetadata =
                (request.Grade.HasValue && request.Grade.Value != 0) &&
                !string.IsNullOrWhiteSpace(request.Chapter);

            if (anyMetadata && !allMetadata)
                return BadRequest("If you provide either Grade or Chapter, you must provide both.");

            // Determine folder path
            string folder = allMetadata
                ? $"{request.Category}/Grade{request.Grade}/{request.Chapter}"
                : $"{request.Category}/Default";

            var url = await _cloudinary.UploadImageAsync(file, folder);

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

        [HttpGet("images")]
        public async Task<IActionResult> SearchImages(
            [FromQuery] string category,
            [FromQuery] string grade,
            [FromQuery] string chapter,
            [FromQuery] int limit = 20)
        {
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