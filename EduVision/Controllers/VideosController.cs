using EduVision.DBContext;
using EduVision.Models;
using EduVision.Models.Constants;
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
        [ProducesResponseType(typeof(ApiResponse<int>), HttpStatusCodes.Accepted)]
        public async Task<ActionResult> CreateVideo([FromBody] EducationRequestDto request)
        {
            var userId = GetAuthenticatedUserId(); // Fixed: removed unnecessary async
            if (userId == null)
                return Unauthorized(ApiResponse<string>.Fail(ErrorMessages.Auth.UserIdNotFound, HttpStatusCodes.Unauthorized));

            var user = await _dbContext.Users.FindAsync(userId.Value);
            if (user == null)
                return NotFound(ApiResponse<string>.Fail(ErrorMessages.Auth.UserDoesNotExist, HttpStatusCodes.NotFound));

            // Enforce video quota for non-manager users
            if ((Role)user.Role != Role.MANAGER)
            {
                bool hasQuota = await _quotaService.CheckQuotaAsync(userId.Value, QuotaTypes.Video);
                if (!hasQuota)
                    return StatusCode(HttpStatusCodes.Forbidden, 
                        ApiResponse<string>.Fail(ErrorMessages.Quota.VideoQuotaExceeded, HttpStatusCodes.Forbidden));
            }

            if (string.IsNullOrEmpty(request.Subject) || string.IsNullOrEmpty(request.Chapter))
                return BadRequest(ApiResponse<string>.Fail(ErrorMessages.Validation.SubjectAndChapterRequired, HttpStatusCodes.BadRequest));

            _logger.LogInformation("Starting video lesson generation for user: {UserId}, subject: {Subject}, chapter: {Chapter}, grade: {Grade}",
                userId.Value, request.Subject, request.Chapter, request.Grade);

            try
            {
                // Create database entry with "Processing" status
                var promptEntity = new Prompt
                {
                    UserId = userId.Value,
                    Content = CreatePromptContent(request, "video"),
                    CreatedAt = DateTime.UtcNow,
                    Status = StatusConstants.ProcessingStatus.Processing
                };
                _dbContext.Prompts.Add(promptEntity);
                await _dbContext.SaveChangesAsync();

                // Create initial slide entries with "Processing" status
                var slideEntity = new Slide
                {
                    PromptId = promptEntity.Promptid,
                    UserId = userId.Value,
                    Type = request.Subject,
                    Status = StatusConstants.ProcessingStatus.Processing
                };
                _dbContext.Slides.Add(slideEntity);
                await _dbContext.SaveChangesAsync();

                // Create initial video entry with "Processing" status
                var videoEntity = new GeneratedVideo
                {
                    PromptId = promptEntity.Promptid,
                    SlideId = slideEntity.SlideId,
                    Status = StatusConstants.ProcessingStatus.Processing,
                    CreatedAt = DateTime.UtcNow,
                    UserId = userId.Value
                };
                _dbContext.GeneratedVideos.Add(videoEntity);
                await _dbContext.SaveChangesAsync();

                // Increment quota - charge upfront to prevent abuse
                await _quotaService.IncrementQuotaUsedAsync(userId.Value, QuotaTypes.Video);

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
                    Message = SuccessMessages.Video.GenerationSubmitted,
                    CreatedAt = DateTime.UtcNow
                };
                _dbContext.Notifications.Add(notification);
                await _dbContext.SaveChangesAsync();

                return Accepted(ApiResponse<int>.Success(
                    promptEntity.Promptid,
                    SuccessMessages.Video.GenerationAccepted));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initiating video generation for user: {UserId}", userId.Value);
                    return StatusCode(HttpStatusCodes.InternalServerError, 
                    ApiResponse<string>.Fail($"{ErrorMessages.Processing.FailedToStartVideoGeneration}: {ex.Message}", 
                    HttpStatusCodes.InternalServerError));
            }
        }

        /// <summary>
        /// Get all videos created by the authenticated user.
        /// </summary>
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetMyVideos(
            [FromQuery] int page = ServiceConstants.Pagination.MinPage, 
            [FromQuery] int pageSize = ServiceConstants.Pagination.DefaultPageSize,
            [FromQuery] string? status = null)
        {
            var userId = GetAuthenticatedUserId();
            if (userId == null)
                return Unauthorized(ApiResponse<string>.Fail(ErrorMessages.Auth.UserIdNotFound, HttpStatusCodes.Unauthorized));

            // Validate pagination
            page = ValidatePage(page);
            pageSize = ValidatePageSize(pageSize);

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
                PromptContent = v.Prompt.Content
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

        #region Private Helper Methods

        private int? GetAuthenticatedUserId()
        {
            var userIdClaim = User.FindFirst("userId") ?? User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
                return null;
            return userId;
        }

        private static string CreatePromptContent(EducationRequestDto request, string type)
        {
            return $"{request.Subject} - {request.Chapter} - Grade {request.Grade} - Template {request.Template} - {type}";
        }

        private static int ValidatePage(int page)
        {
            return page < ServiceConstants.Pagination.MinPage ? ServiceConstants.Pagination.MinPage : page;
        }

        private static int ValidatePageSize(int pageSize)
        {
            if (pageSize < 1 || pageSize > ServiceConstants.Pagination.MaxPageSize)
                return ServiceConstants.Pagination.DefaultPageSize;
            return pageSize;
        }

        #endregion
    }
}