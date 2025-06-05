using EduVision.Services;
using EduVision.DBContext;
using EduVision.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EduVision.Models.DTO;

namespace EduVision.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MediaController : ControllerBase
    {
        private readonly CloudinaryImageService _cloudinary;
        private readonly EduVisionContext _dbContext;

        public MediaController(CloudinaryImageService cloudinary, EduVisionContext dbContext)
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

            if (!request.UserId.HasValue)
                return BadRequest("UserId is required.");

            // If any metadata (except UserId) is provided, require all of them
            bool anyMetadata =
                (request.Grade.HasValue && request.Grade != 0) ||
                (!string.IsNullOrWhiteSpace(request.Chapter) && request.Chapter != "string") ||
                (!string.IsNullOrWhiteSpace(request.Session) && request.Session != "string") ||
                (!string.IsNullOrWhiteSpace(request.Class) && request.Class != "string") ||
                (!string.IsNullOrWhiteSpace(request.Semester) && request.Semester != "string");

            bool allMetadata =
                (request.Grade.HasValue && request.Grade != 0) &&
                (!string.IsNullOrWhiteSpace(request.Chapter) && request.Chapter != "string") &&
                (!string.IsNullOrWhiteSpace(request.Session) && request.Session != "string") &&
                (!string.IsNullOrWhiteSpace(request.Class) && request.Class != "string") &&
                (!string.IsNullOrWhiteSpace(request.Semester) && request.Semester != "string");

            if (anyMetadata && !allMetadata)
                return BadRequest("If you provide any of Grade, Chapter, Session, Class, or Semester, you must provide all of them.");

            // Determine folder path
            string folder = allMetadata
                ? $"{request.Category}/{request.Grade}/{request.Chapter}/Session: {request.Session}"
                : $"{request.Category}/Default";

            var url = await _cloudinary.UploadImageAsync(file, folder);

            var image = new Image
            {
                UserId = request.UserId,
                Url = url,
                Status = "Active",
                Category = request.Category,
                Chapter = string.IsNullOrWhiteSpace(request.Chapter) || request.Chapter == "string" ? "None" : request.Chapter,
                Session = string.IsNullOrWhiteSpace(request.Session) || request.Session == "string" ? "None" : request.Session,
                Class = string.IsNullOrWhiteSpace(request.Class) || request.Class == "string" ? "None" : request.Class,
                Semester = string.IsNullOrWhiteSpace(request.Semester) || request.Semester == "string" ? "None" : request.Semester
            };
            _dbContext.Images.Add(image);
            await _dbContext.SaveChangesAsync();

            return Ok(new { imageUrl = url, imageId = image.ImageId });
        }

        [HttpGet("images")]
        public async Task<IActionResult> SearchImages(
            [FromQuery] string category,
            [FromQuery] int? userId,
            [FromQuery] string grade, // Accept grade as string, e.g., "Grade12"
            [FromQuery] string chapter,
            [FromQuery] string session,
            [FromQuery] string @class,
            [FromQuery] string semester,
            [FromQuery] int limit = 20)
        {
            var query = _dbContext.Images.AsQueryable();

            if (!string.IsNullOrWhiteSpace(grade) && grade != "grade")
                query = query.Where(i => i.Class == grade);
            else
                query = query.Where(i => i.Class == "None");

            if (!string.IsNullOrWhiteSpace(chapter) && chapter != "chapter")
                query = query.Where(i => i.Chapter == chapter);
            else
                query = query.Where(i => i.Chapter == "None");

            if (!string.IsNullOrWhiteSpace(session) && session != "session")
                query = query.Where(i => i.Session == session);
            else
                query = query.Where(i => i.Session == "None");

            if (!string.IsNullOrWhiteSpace(@class) && @class != "class")
                query = query.Where(i => i.Class == @class);
            // No else needed, already handled by grade above

            if (!string.IsNullOrWhiteSpace(semester) && semester != "semester")
                query = query.Where(i => i.Semester == semester);
            else
                query = query.Where(i => i.Semester == "None");

            var images = await query
            .OrderByDescending(i => i.ImageId)
            .Take(limit)
            .Select(i => new {
                i.ImageId,
                i.UserId,
                i.Url,
                i.Status,
                i.Chapter,
                i.Category,
                i.Semester,
                i.Session,
                i.Class
        })
        .ToListAsync();

            return Ok(images);
        }
    }
}