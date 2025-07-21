using EduVision.Models.Constants; 
using EduVision.Models.DTO.Response;
using EduVision.Services.Data;
using Microsoft.AspNetCore.Mvc;

namespace EduVision.Controllers
{
    /// <summary>
    /// Manages educational curriculum data including subjects and chapters.
    /// </summary>
    [ApiController]
    [Route("api/curriculum")]
    public class CurriculumController : ControllerBase
    {
        private readonly IMongoDbService _mongoDbService;
        private readonly ILogger<CurriculumController> _logger;

        public CurriculumController(IMongoDbService mongoDbService, ILogger<CurriculumController> logger)
        {
            _mongoDbService = mongoDbService;
            _logger = logger;
        }

        /// <summary>
        /// Get all available subjects for lesson generation.
        /// </summary>
        [HttpGet("subjects")]
        [ResponseCache(Duration = ServiceConstants.Cache.SubjectsCacheDuration)] 
        public async Task<IActionResult> GetSubjects()
        {
            try
            {
                var subjects = await _mongoDbService.GetAvailableSubjectsAsync();
                return Ok(ApiResponse<List<string>>.Success(subjects));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching subjects");
                return StatusCode(HttpStatusCodes.InternalServerError, 
                    ApiResponse<string>.Fail($"Failed to fetch subjects: {ex.Message}", HttpStatusCodes.InternalServerError));
            }
        }

        /// <summary>
        /// Get all chapters for a given subject and grade.
        /// </summary>
        [HttpGet("chapters")]
        [ResponseCache(Duration = ServiceConstants.Cache.ChaptersCacheDuration)] 
        public async Task<IActionResult> GetChapters([FromQuery] string subject, [FromQuery] int? grade = null)
        {
            if (string.IsNullOrEmpty(subject))
            {
                return BadRequest(ApiResponse<string>.Fail(ErrorMessages.Validation.SubjectRequired, HttpStatusCodes.BadRequest));
            }

            try
            {
                var chapters = await _mongoDbService.GetChaptersAsync(subject, grade);
                return Ok(ApiResponse<List<string>>.Success(chapters));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching chapters for subject: {Subject}", subject);
                return StatusCode(HttpStatusCodes.InternalServerError, 
                    ApiResponse<string>.Fail($"Failed to fetch chapters: {ex.Message}", HttpStatusCodes.InternalServerError));
            }
        }
    }
}