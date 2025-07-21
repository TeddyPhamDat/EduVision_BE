using EduVision.DBContext;
using EduVision.Models;
using EduVision.Models.Constants; // Add this
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
    /// Manages slide generation and retrieval operations.
    /// </summary>
    [ApiController]
    [Route("api/slides")]
    public class SlidesController : ControllerBase
    {
        private readonly EduVisionContext _dbContext;
        private readonly IQuotaService _quotaService;
        private readonly KafkaProducerService _kafkaProducerService;

        public SlidesController(
            EduVisionContext dbContext,
            IQuotaService quotaService,
            KafkaProducerService kafkaProducerService)
        {
            _dbContext = dbContext;
            _quotaService = quotaService;
            _kafkaProducerService = kafkaProducerService;
        }

        /// <summary>
        /// Generate slides for a lesson.
        /// </summary>
        [Authorize(Roles = "USER,MANAGER,ADMIN")]
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<int>), HttpStatusCodes.Accepted)]
        public async Task<ActionResult> CreateSlides([FromBody] EducationRequestDto request)
        {
            var userId = GetAuthenticatedUserId(); 
            if (userId == null)
                return Unauthorized(ApiResponse<string>.Fail(ErrorMessages.Auth.UserIdNotFound, HttpStatusCodes.Unauthorized));

            var user = await _dbContext.Users.FindAsync(userId.Value);
            if (user == null)
                return NotFound(ApiResponse<string>.Fail(ErrorMessages.Auth.UserDoesNotExist, HttpStatusCodes.NotFound));

            // Check quota for non-managers
            if ((Role)user.Role != Role.MANAGER)
            {
                bool hasQuota = await _quotaService.CheckQuotaAsync(userId.Value, QuotaTypes.Slides);
                if (!hasQuota)
                    return StatusCode(HttpStatusCodes.Forbidden, 
                        ApiResponse<string>.Fail(ErrorMessages.Quota.SlideQuotaExceeded, HttpStatusCodes.Forbidden));
            }

            if (string.IsNullOrEmpty(request.Subject) || string.IsNullOrEmpty(request.Chapter))
                return BadRequest(ApiResponse<string>.Fail(ErrorMessages.Validation.SubjectAndChapterRequired, HttpStatusCodes.BadRequest));

            // Create database entry with "Processing" status
            var promptEntity = new Prompt
            {
                UserId = userId.Value,
                Content = CreatePromptContent(request, "slides"),
                CreatedAt = DateTime.UtcNow,
                Status = StatusConstants.ProcessingStatus.Processing
            };
            _dbContext.Prompts.Add(promptEntity);
            await _dbContext.SaveChangesAsync();

            // Enqueue the job to Kafka
            await _kafkaProducerService.ProduceAsync(new SlideGenerationKafkaMessage
            {
                UserId = userId.Value,
                Request = request,
                PromptId = promptEntity.Promptid
            });

            // Create a notification for the user
            var notification = new Notification
            {
                UserId = userId.Value,
                Message = SuccessMessages.Slides.GenerationSubmitted,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.Notifications.Add(notification);
            await _dbContext.SaveChangesAsync();

            return Accepted(ApiResponse<int>.Success(promptEntity.Promptid, SuccessMessages.Slides.GenerationAccepted));
        }

        /// <summary>
        /// Get all slides created by the authenticated user.
        /// </summary>
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetMySlides(
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

            var query = _dbContext.Slides
                .Include(s => s.Prompt)
                .Where(s => s.UserId == userId.Value);

            // Apply status filter if provided
            if (!string.IsNullOrEmpty(status))
                query = query.Where(s => s.Status == status);

            var totalCount = await query.CountAsync();
            var slides = await query
                .OrderByDescending(s => s.SlideId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var slideResponses = slides.Select(s => new SlideResponse
            {
                SlideId = s.SlideId,
                PromptId = s.PromptId,
                Type = s.Type,
                Url = s.Url,
                Status = s.Status,
                PromptContent = s.Prompt.Content
            }).ToList();

            var paginatedResponse = new PaginatedResponse<SlideResponse>
            {
                Data = slideResponses,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            };

            return Ok(ApiResponse<PaginatedResponse<SlideResponse>>.Success(paginatedResponse));
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