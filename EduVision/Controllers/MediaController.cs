using EduVision.Services;
using Microsoft.AspNetCore.Mvc;

namespace EduVision.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MediaController : ControllerBase
    {
        private readonly CloudinaryImageService _cloudinary;

        public MediaController(CloudinaryImageService cloudinary)
        {
            _cloudinary = cloudinary;
        }

        [HttpPost("upload-image")]
        public async Task<IActionResult> UploadImage(
            IFormFile file,
            [FromQuery] string category)
        {
            if (file == null || string.IsNullOrWhiteSpace(category))
                return BadRequest("File and category are required.");

            var url = await _cloudinary.UploadImageAsync(file, category);
            return Ok(new { imageUrl = url });
        }

        [HttpGet("images")]
        public async Task<IActionResult> GetImages([FromQuery] string category, [FromQuery] int limit = 5)
        {
            if (string.IsNullOrWhiteSpace(category))
                return BadRequest("Category is required.");

            var urls = await _cloudinary.GetImagesByCategoryAsync(category, limit);
            return Ok(urls);
        }
    }
}