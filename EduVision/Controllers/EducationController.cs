using EduVision.DBContext;
using EduVision.Models;
using EduVision.Models.DTO;
using EduVision.Models.DTO.Request;
using EduVision.Models.DTO.Response;
using EduVision.Models.Entities.Enum;
using EduVision.Services.AI;
using EduVision.Services.Data;
using EduVision.Services.Media;
using EduVision.Services.Messaging;
using EduVision.Services.Presentation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Security.Claims;

namespace EduVision.Controllers
{
    // The EducationController provides endpoints for generating educational content.
    // The API is split into two main endpoints: one for slide-only generation and one for full video lessons.
    // This separation is necessary to clearly distinguish between use cases and to simplify client integration.
    [ApiController]
    [Route("api/education")]
    public class EducationController : ControllerBase
    {
        // Dependencies are injected to follow the Dependency Injection principle.
        // This makes the controller easier to test and maintain, and allows for flexible service replacement.

        private readonly IMongoDbService _mongoDbService;
        private readonly EduVisionContext _dbContext;
        private readonly ILogger<EducationController> _logger;
        private readonly IQuotaService _quotaService;
        private readonly KafkaProducerService _kafkaProducerService;

        // Constructor injects all required services for lesson generation, storage, and quota management.
        // This ensures all business logic is handled by dedicated services, keeping the controller thin.
        public EducationController(
            IMongoDbService mongoDbService,
            EduVisionContext dbContext,
            ILogger<EducationController> logger,
            IQuotaService quotaService,
            KafkaProducerService kafkaProducerService)
        {

            _mongoDbService = mongoDbService;
            _dbContext = dbContext;
            _logger = logger;
            _quotaService = quotaService;
            _kafkaProducerService = kafkaProducerService;
        }

        /// <summary>
        /// Get all available subjects for lesson generation.
        /// </summary>
        // Why: Allows clients to dynamically display subject options to users, ensuring up-to-date and relevant content selection.
        [HttpGet("subjects")]
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
                return StatusCode(500, ApiResponse<string>.Fail($"Failed to fetch subjects: {ex.Message}", 500));
            }
        }

        /// <summary>
        /// Get all chapters for a given subject and grade.
        /// </summary>
        // Why: Enables clients to present chapter options based on the selected subject and grade, supporting lesson customization and navigation.
        [HttpGet("chapters")]
        public async Task<IActionResult> GetChapters([FromQuery] string subject, [FromQuery] int? grade = null)
        {
            if (string.IsNullOrEmpty(subject))
            {
                return BadRequest(ApiResponse<string>.Fail("Subject parameter is required", 400));
            }

            try
            {
                var chapters = await _mongoDbService.GetChaptersAsync(subject, grade);
                return Ok(ApiResponse<List<string>>.Success(chapters));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching chapters for subject: {Subject}", subject);
                return StatusCode(500, ApiResponse<string>.Fail($"Failed to fetch chapters: {ex.Message}", 500));
            }
        }

        /// <summary>
        /// Generate slides for a lesson.
        /// </summary>
        // Why: This endpoint is for users who only need visual learning materials, not full video lessons.
        [Authorize(Roles = "USER,MANAGER,ADMIN")]
        [HttpPost("slides")]
        [ProducesResponseType(typeof(SlideGenerationResultDto), 202)]
        public async Task<ActionResult<SlideGenerationResultDto>> CreateSlides([FromBody] EducationRequestDto request)
        {
            var userIdClaim = User.FindFirst("userId") ?? User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized(ApiResponse<string>.Fail("User ID not found in token", 401));

            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null)
                return BadRequest(ApiResponse<string>.Fail("User does not exist", 404));
            if ((Role)user.Role != Role.MANAGER)
            {
                bool hasQuota = await _quotaService.CheckQuotaAsync(userId, "slides");
                if (!hasQuota)
                    return BadRequest(ApiResponse<string>.Fail("You have exceeded the number of slide generations for the month", 400));
            }

            if (string.IsNullOrEmpty(request.Subject) || string.IsNullOrEmpty(request.Chapter))
                return BadRequest(ApiResponse<string>.Fail("Subject and chapter parameters are required", 400));

            // Create database entry with "Processing" status
            var promptEntity = new Prompt
            {
                UserId = userId,
                Content = $"{request.Subject} - {request.Chapter} - Grade {request.Grade} - Template {request.Template} - slides",
                CreatedAt = DateTime.UtcNow,
                Status = "Processing"
            };
            _dbContext.Prompts.Add(promptEntity);
            await _dbContext.SaveChangesAsync();

            // Enqueue the job to Kafka
            await _kafkaProducerService.ProduceAsync(new SlideGenerationKafkaMessage
            {
                UserId = userId,
                Request = request,
                PromptId = promptEntity.Promptid
            });

            // Return 202 Accepted
            return Accepted(ApiResponse<int>.Success(promptEntity.Promptid, "Slide generation request accepted and is being processed."));
        }

        /// <summary>
        /// Get all slides created by the authenticated user.
        /// </summary>
        [Authorize]
        [HttpGet("slides")]
        public async Task<IActionResult> GetMySlides()
        {
            var userIdClaim = User.FindFirst("userId") ?? User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized(ApiResponse<string>.Fail("User ID not found in token", 401));

            var slides = await _dbContext.Slides
                .Include(s => s.Prompt)
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.SlideId)
                .ToListAsync();


            var slideResponses = slides.Select(s => new SlideResponse
            {
                SlideId = s.SlideId,
                PromptId = s.PromptId,
                Type = s.Type,
                Url = s.Url,
                Status = s.Status,
                PromptContent = s.Prompt?.Content // Lấy content từ Prompt
            }).ToList();

            return Ok(ApiResponse<List<SlideResponse>>.Success(slideResponses));
        }

        /// <summary>
        /// Get all videos created by the authenticated user.
        /// </summary>
        [Authorize]
        [HttpGet("videos")]
        public async Task<IActionResult> GetMyVideos()
        {
            var userIdClaim = User.FindFirst("userId") ?? User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized(ApiResponse<string>.Fail("User ID not found in token", 401));

            var videos = await _dbContext.GeneratedVideos
                .Include(s => s.Prompt)
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.SlideId)
                .ToListAsync();


            var videoResponses = videos.Select(v => new VideoResponse
            {
                GenerateVideoId = v.GenerateVideoId,
                Status = v.Status,
                CreatedAt = v.CreatedAt,
                VideoUrl = v.VideoUrl,
                PromptContent = v.Prompt?.Content // Lấy content từ Prompt
            }).ToList();

            return Ok(ApiResponse<List<VideoResponse>>.Success(videoResponses));
        }


/// <summary>
/// Gets the latest slide status for a given user.
/// </summary>
        [HttpGet("slide-status/{userId}")]
        public async Task<IActionResult> GetLatestSlideStatus(int userId)
        {
            var latestPrompt = await _dbContext.Prompts
                .Where(p => p.UserId == userId)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync();

            if (latestPrompt == null)
                return NotFound(ApiResponse<string>.Fail("No prompt found for this user", 404));

            var slide = await _dbContext.Slides
                .Where(s => s.PromptId == latestPrompt.Promptid)
                .FirstOrDefaultAsync();

            if (slide == null)
                return Ok(ApiResponse<object>.Success(new {
                    Status = "Processing",
                    Url = (string)""
                }, "Slide is being generated"));

            return Ok(ApiResponse<object>.Success(new {
                Status = slide.Status,
                Url = slide.Url
            }, "Slide status fetched successfully"));
        }


/// <summary>
/// Gets the latest video status for a given user.
/// </summary>
        [HttpGet("video-status/{userId}")]
        public async Task<IActionResult> GetLatestVideoStatus(int userId)
        {
            var latestPrompt = await _dbContext.Prompts
                .Where(p => p.UserId == userId)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync();

            if (latestPrompt == null)
                return NotFound(ApiResponse<string>.Fail("No prompt found for this user", 404));

            var video = await _dbContext.GeneratedVideos
                .Where(v => v.PromptId == latestPrompt.Promptid)
                .FirstOrDefaultAsync();

            if (video == null)
                return Ok(ApiResponse<object>.Success(new {
                    Status = "Processing",
                    Url = (string)null
                }, "Video is being generated"));

            return Ok(ApiResponse<object>.Success(new {
                Status = video.Status,
                Url = video.VideoUrl
            }, "Video status fetched successfully"));
        }

        /// <summary>
        /// Generate a full video lesson (with audio).
        /// </summary>
        // Why: This endpoint is for users who want a complete multimedia lesson, including narration and video composition.
        [Authorize(Roles = "USER,MANAGER,ADMIN")]
        [HttpPost("videos")]
        [ProducesResponseType(typeof(ApiResponse<int>), 202)]
        public async Task<ActionResult> CreateVideoLesson([FromBody] EducationRequestDto request)
        {
            // Extract user ID from JWT claims for quota and ownership checks
            var userIdClaim = User.FindFirst("userId") ?? User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized(ApiResponse<string>.Fail("User ID not found in token", 401));
            }

            // Enforce video quota for non-manager users
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null)
                return BadRequest(ApiResponse<string>.Fail("User does not exist", 404));
            if ((Role)user.Role != Role.MANAGER)
            {
                bool hasQuota = await _quotaService.CheckQuotaAsync(userId, "video");
                if (!hasQuota)
                    return BadRequest(ApiResponse<string>.Fail("You have exceeded the number of video generations for the month", 400));
            }

            // Validate required parameters
            if (string.IsNullOrEmpty(request.Subject) || string.IsNullOrEmpty(request.Chapter))
                return BadRequest(ApiResponse<string>.Fail("Subject and chapter parameters are required", 400));

            _logger.LogInformation("Starting video lesson generation for subject: {Subject}, chapter: {Chapter}, grade: {Grade}",
                request.Subject, request.Chapter, request.Grade);

            try
            {
                // Create database entry with "Processing" status
                var promptEntity = new Prompt
                {
                    UserId = userId,
                    Content = $"{request.Subject} - {request.Chapter} - Grade {request.Grade} - Template {request.Template} - video",
                    CreatedAt = DateTime.UtcNow,
                    Status = "Processing" // Important: Mark as processing until video is completed
                };
                _dbContext.Prompts.Add(promptEntity);
                await _dbContext.SaveChangesAsync();

                // Create initial slide entries with "Processing" status
                var slideEntity = new Slide
                {
                    PromptId = promptEntity.Promptid,
                    UserId = userId,
                    Type = request.Subject,
                    Status = "Processing"
                };
                _dbContext.Slides.Add(slideEntity);
                await _dbContext.SaveChangesAsync();

                // Create initial video entry with "Processing" status
                var videoEntity = new GeneratedVideo
                {
                    PromptId = promptEntity.Promptid,
                    SlideId = slideEntity.SlideId,
                    Status = "Processing",
                    CreatedAt = DateTime.UtcNow
                };
                _dbContext.GeneratedVideos.Add(videoEntity);
                await _dbContext.SaveChangesAsync();

                // Increment quota - we charge upfront to prevent abuse
                await _quotaService.IncrementQuotaUsedAsync(userId, "video");

                // Enqueue the slide generation job with video flag set to true
                await _kafkaProducerService.ProduceAsync(new SlideGenerationKafkaMessage
                {
                    UserId = userId,
                    Request = request,
                    PromptId = promptEntity.Promptid,
                    GenerateVideo = true  // Signal that video should be generated after slides
                });

                // Return 202 Accepted with a tracking ID
                return Accepted(ApiResponse<int>.Success(
                    promptEntity.Promptid, 
                    "Video generation request accepted and is being processed. Use the returned ID to check the status."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initiating video generation");
                return StatusCode(500, ApiResponse<string>.Fail($"Failed to start video generation: {ex.Message}", 500));
            }
        }

    }
}