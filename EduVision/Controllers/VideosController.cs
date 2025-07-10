using EduVision.DBContext;
using EduVision.Models;
using EduVision.Models.DTO.Request;
using EduVision.Models.DTO.Response;
using EduVision.Models.Entities.Enum;
using EduVision.Services.Data;
using EduVision.Services.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EduVision.Controllers
{
    /// <summary>
    /// Manages video lesson generation and retrieval operations.
    /// </summary>
    [ApiController]
    [Route("api/videos")]
    public class VideosController : ControllerBase
    {
        private readonly EduVisionContext _dbContext;
        private readonly ILogger<VideosController> _logger;
        private readonly IQuotaService _quotaService;
        private readonly KafkaProducerService _kafkaProducerService;

        public VideosController(
            EduVisionContext dbContext,
            ILogger<VideosController> logger,
            IQuotaService quotaService,
            KafkaProducerService kafkaProducerService)
        {
            _dbContext = dbContext;
            _logger = logger;
            _quotaService = quotaService;
            _kafkaProducerService = kafkaProducerService;
        }

        /// <summary>
        /// Generate a full video lesson (with audio).
        /// </summary>
        [Authorize(Roles = "USER,MANAGER,ADMIN")]
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<int>), 202)]
        public async Task<ActionResult> CreateVideo([FromBody] EducationRequestDto request)
        {
            var userId = await GetAuthenticatedUserIdAsync();
            if (userId == null)
                return Unauthorized(ApiResponse<string>.Fail("User ID not found in token", 401));

            var user = await _dbContext.Users.FindAsync(userId.Value);
            if (user == null)
                return NotFound(ApiResponse<string>.Fail("User does not exist", 404));

            // Enforce video quota for non-manager users
            if ((Role)user.Role != Role.MANAGER)
            {
                bool hasQuota = await _quotaService.CheckQuotaAsync(userId.Value, "video");
                if (!hasQuota)
                    return StatusCode(403, ApiResponse<string>.Fail("You have exceeded the number of video generations for the month", 403));
            }

            if (string.IsNullOrEmpty(request.Subject) || string.IsNullOrEmpty(request.Chapter))
                return BadRequest(ApiResponse<string>.Fail("Subject and chapter parameters are required", 400));

            _logger.LogInformation("Starting video lesson generation for user: {UserId}, subject: {Subject}, chapter: {Chapter}, grade: {Grade}",
                userId.Value, request.Subject, request.Chapter, request.Grade);

            try
            {
                // Create database entry with "Processing" status
                var promptEntity = new Prompt
                {
                    UserId = userId.Value,
                    Content = $"{request.Subject} - {request.Chapter} - Grade {request.Grade} - Template {request.Template} - video",
                    CreatedAt = DateTime.UtcNow,
                    Status = "Processing"
                };
                _dbContext.Prompts.Add(promptEntity);
                await _dbContext.SaveChangesAsync();

                // Create initial slide entries with "Processing" status
                var slideEntity = new Slide
                {
                    PromptId = promptEntity.Promptid,
                    UserId = userId.Value,
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
                    CreatedAt = DateTime.UtcNow,
                    UserId = userId.Value
                };
                _dbContext.GeneratedVideos.Add(videoEntity);
                await _dbContext.SaveChangesAsync();

                // Increment quota - charge upfront to prevent abuse
                await _quotaService.IncrementQuotaUsedAsync(userId.Value, "video");

                // Enqueue the slide generation job with video flag set to true
                await _kafkaProducerService.ProduceAsync(new SlideGenerationKafkaMessage
                {
                    UserId = userId.Value,
                    Request = request,
                    PromptId = promptEntity.Promptid,
                    GenerateVideo = true
                });

                // Create a notification for the user
                var notification = new Notification
                {
                    UserId = userId.Value,
                    Message = "Your video generation request has been submitted and is being processed.",
                    CreatedAt = DateTime.UtcNow
                };
                _dbContext.Notifications.Add(notification);
                await _dbContext.SaveChangesAsync();

                return Accepted(ApiResponse<int>.Success(
                    promptEntity.Promptid,
                    "Video generation request accepted and is being processed. Use the returned ID to check the status."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initiating video generation for user: {UserId}", userId.Value);
                return StatusCode(500, ApiResponse<string>.Fail($"Failed to start video generation: {ex.Message}", 500));
            }
        }

        /// <summary>
        /// Get all videos created by the authenticated user.
        /// </summary>
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetMyVideos(
            [FromQuery] int page = 1, 
            [FromQuery] int pageSize = 10,
            [FromQuery] string? status = null)
        {
            var userId = await GetAuthenticatedUserIdAsync();
            if (userId == null)
                return Unauthorized(ApiResponse<string>.Fail("User ID not found in token", 401));

            // Validate pagination
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 10;

            var query = _dbContext.GeneratedVideos
                .Include(v => v.Prompt)
                .Where(v => v.UserId == userId.Value);

            // Apply status filter if provided
            if (!string.IsNullOrEmpty(status))
                query = query.Where(v => v.Status == status);

            var totalCount = await query.CountAsync();
            var videos = await query
                .OrderByDescending(v => v.GenerateVideoId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var videoResponses = videos.Select(v => new VideoResponse
            {
                GenerateVideoId = v.GenerateVideoId,
                Status = v.Status,
                CreatedAt = v.CreatedAt,
                VideoUrl = v.VideoUrl,
                PromptContent = v.Prompt?.Content
            }).ToList();

            var paginatedResponse = new PaginatedResponse<VideoResponse>
            {
                Data = videoResponses,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            };

            return Ok(ApiResponse<PaginatedResponse<VideoResponse>>.Success(paginatedResponse));
        }

        /// <summary>
        /// Get a specific video by ID.
        /// </summary>
        [Authorize]
        [HttpGet("{videoId:int}")]
        public async Task<IActionResult> GetVideo(int videoId)
        {
            var userId = await GetAuthenticatedUserIdAsync();
            if (userId == null)
                return Unauthorized(ApiResponse<string>.Fail("User ID not found in token", 401));

            var video = await _dbContext.GeneratedVideos
                .Include(v => v.Prompt)
                .Where(v => v.GenerateVideoId == videoId && v.UserId == userId.Value)
                .FirstOrDefaultAsync();

            if (video == null)
                return NotFound(ApiResponse<string>.Fail("Video not found", 404));

            var response = new VideoResponse
            {
                GenerateVideoId = video.GenerateVideoId,
                Status = video.Status,
                CreatedAt = video.CreatedAt,
                VideoUrl = video.VideoUrl,
                PromptContent = video.Prompt?.Content
            };

            return Ok(ApiResponse<VideoResponse>.Success(response));
        }

        /// <summary>
        /// Get the status of a specific video.
        /// </summary>
        [Authorize]
        [HttpGet("{videoId:int}/status")]
        public async Task<IActionResult> GetVideoStatus(int videoId)
        {
            var userId = await GetAuthenticatedUserIdAsync();
            if (userId == null)
                return Unauthorized(ApiResponse<string>.Fail("User ID not found in token", 401));

            var video = await _dbContext.GeneratedVideos
                .Where(v => v.GenerateVideoId == videoId && v.UserId == userId.Value)
                .FirstOrDefaultAsync();

            if (video == null)
                return NotFound(ApiResponse<string>.Fail("Video not found", 404));

            return Ok(ApiResponse<object>.Success(new {
                Status = video.Status,
                Url = video.VideoUrl
            }, "Video status retrieved successfully"));
        }

        private async Task<int?> GetAuthenticatedUserIdAsync()
        {
            var userIdClaim = User.FindFirst("userId") ?? User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
                return null;
            return userId;
        }
    }
}