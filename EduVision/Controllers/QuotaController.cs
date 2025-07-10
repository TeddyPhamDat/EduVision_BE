using EduVision.Models.DTO.Request;
using EduVision.Models.DTO.Response;
using EduVision.Services.Data;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization; // Added for Authorize attribute
using System.Security.Claims; // Added for ClaimTypes
using System; // Added for Exception

namespace EduVision.Controllers
{
    [Authorize] // Added Authorize attribute for the whole controller
    [ApiController]
    [Route("api/quotas")]
    public class QuotasController : ControllerBase
    {
        private readonly IQuotaService _quotaService;

        public QuotasController(IQuotaService quotaService)
        {
            _quotaService = quotaService;
        }

        /// <summary>
        /// Checks if the authenticated user has available quota for a specific resource type.
        /// </summary>
        [HttpGet("availability")]
        public async Task<IActionResult> CheckQuota([FromQuery] string quotaType)
        {
            var userId = await GetAuthenticatedUserIdAsync();
            if (userId == null)
                return Unauthorized(ApiResponse<string>.Fail("User ID not found in token", 401));

            var canUse = await _quotaService.CheckQuotaAsync(userId.Value, quotaType);
            var result = new { CanUse = canUse };
            var message = canUse ? "Quota is available." : "Quota limit reached.";
            return Ok(ApiResponse<object>.Success(result, message));
        }

        /// <summary>
        /// Consumes (increments) the authenticated user's quota usage for a specific resource type.
        /// </summary>
        [HttpPost("usage")]
        public async Task<IActionResult> UseQuota([FromBody] QuotaRequest request)
        {
            var userId = await GetAuthenticatedUserIdAsync();
            if (userId == null)
                return Unauthorized(ApiResponse<string>.Fail("User ID not found in token", 401));
            
            // Ensure the request's userId matches the authenticated user's ID for security
            if (request.UserId != userId.Value)
                return Unauthorized(ApiResponse<string>.Fail("Cannot use quota for another user", 403));

            await _quotaService.IncrementQuotaUsedAsync(request.UserId, request.QuotaType);
            return Ok(ApiResponse<object>.Success("", "Quota used successfully."));
        }

        /// <summary>
        /// Retrieves the quota usage history for the authenticated user.
        /// </summary>
        [HttpGet("history")]
        public async Task<IActionResult> GetQuotaHistory()
        {
            var userId = await GetAuthenticatedUserIdAsync();
            if (userId == null)
                return Unauthorized(ApiResponse<string>.Fail("User ID not found in token", 401));

            var quotaHistory = await _quotaService.GetQuotaHistoryAsync(userId.Value);

            if (quotaHistory == null || quotaHistory.Count == 0)
                return NotFound(ApiResponse<object>.Fail("No quota history found for this user", 404));

            return Ok(ApiResponse<object>.Success(quotaHistory));
        }

        /// <summary>
        /// Retrieves the quota summary for the authenticated user for the current period.
        /// </summary>
        [HttpGet("summary")]
        public async Task<IActionResult> GetQuotaSummary()
        {
            var userId = await GetAuthenticatedUserIdAsync();
            if (userId == null)
                return Unauthorized(ApiResponse<string>.Fail("User ID not found in token", 401));

            var quotaSummary = await _quotaService.GetQuotaSummaryAsync(userId.Value);
            
            return Ok(ApiResponse<QuotaSummaryResponse>.Success(quotaSummary));
        }

        /// <summary>
        /// [Admin Only] Manually updates a user's quota for a specific resource type.
        /// This API is intended for administrative purposes to adjust user quotas.
        /// </summary>
        [HttpPost("admin/update")]
        [Authorize(Roles = "ADMIN")] // Ensure only ADMIN can access
        public async Task<IActionResult> AdminUpdateQuota([FromBody] QuotaUpdateRequest request)
        {
            try
            {
                // Validate QuotaType to prevent arbitrary strings
                if (request.QuotaType != "slides" && request.QuotaType != "video")
                {
                    return BadRequest(ApiResponse<string>.Fail("Invalid QuotaType. Must be 'slides' or 'video'.", 400));
                }

                await _quotaService.UpdateQuotaAsync(request.UserId, request.QuotaType, request.Amount);
                return Ok(ApiResponse<string>.Success("", "Quota updated successfully."));
            }
            catch (Exception ex)
            {
                // Log the exception for debugging
                // _logger.LogError(ex, "Error updating quota for user {UserId} with type {QuotaType}", request.UserId, request.QuotaType);
                return StatusCode(500, ApiResponse<string>.Fail($"Failed to update quota: {ex.Message}", 500));
            }
        }

        // Helper method to get authenticated user ID
        private async Task<int?> GetAuthenticatedUserIdAsync()
        {
            var userIdClaim = User.FindFirst("userId") ?? User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
                return null;
            return userId;
        }
    }
}