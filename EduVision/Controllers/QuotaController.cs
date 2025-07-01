using EduVision.Models.DTO.Request;
using EduVision.Models.DTO.Response;
using EduVision.Services.Data;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;

namespace EduVision.Controllers
{
    // QuotaController provides endpoints for checking and updating user quotas (e.g., for slides or video generation).
    // API naming: Follows RESTful conventions with plural, hyphenated nouns (e.g., /api/quotas).
    // Why: Centralizes all quota-related logic, making it easy for clients to check and consume quota in a consistent way.
    [ApiController]
    [Route("api/quotas")]
    public class QuotasController : ControllerBase
    {
        private readonly IQuotaService _quotaService;

        // Injects the quota service abstraction.
        // Why: Enables flexible quota logic and easier testing or replacement.
        public QuotasController(IQuotaService quotaService)
        {
            _quotaService = quotaService;
        }

        /// <summary>
        /// Checks if a user has available quota for a specific resource type (e.g., "slides" or "video").
        /// </summary>
        [HttpGet("availability")]
        public async Task<IActionResult> CheckQuota([FromQuery] int userId, [FromQuery] string quotaType)
        {
            // Calls the quota service to check if the user has remaining quota for the requested type.
            // Why: Prevents users from exceeding their monthly or per-period limits.
            var canUse = await _quotaService.CheckQuotaAsync(userId, quotaType);
            var result = new { CanUse = canUse };
            var message = canUse ? "Quota is available." : "Quota limit reached.";
            return Ok(ApiResponse<object>.Success(result, message));
        }

        /// <summary>
        /// Consumes (increments) a user's quota usage for a specific resource type.
        /// </summary>
        [HttpPost("usage")]
        public async Task<IActionResult> UseQuota([FromBody] QuotaRequest request)
        {
            // Increments the quota usage for the specified user and resource type.
            // Why: Keeps quota usage in sync with actual resource consumption.
            await _quotaService.IncrementQuotaUsedAsync(request.UserId, request.QuotaType);
            return Ok(ApiResponse<object>.Success("", "Quota used successfully."));
        }

        /// <summary>
        /// Retrieves the quota usage history for a specific user.
        /// </summary>
        [HttpGet("history")]
        public async Task<IActionResult> GetQuotaHistory([FromQuery] int userId)
        {
            var quotaHistory = await _quotaService.GetQuotaHistoryAsync(userId);

            if (quotaHistory == null || quotaHistory.Count == 0)
                return NotFound(ApiResponse<object>.Fail("No quota history found for this user", 404));

            return Ok(ApiResponse<object>.Success(quotaHistory));
        }

        ///// <summary>
        ///// [Admin Only] Manually updates a user's quota for a specific resource type.
        ///// This API is intended for administrative purposes to adjust user quotas.
        ///// </summary>
        //[HttpPost("admin/update")]
        //// [Authorize(Roles = "Admin")] // Uncomment this line and ensure proper authorization setup
        //public async Task<IActionResult> AdminUpdateQuota([FromBody] QuotaUpdateRequest request)
        //{
        //    // TODO: Implement the logic to update the user's quota.
        //    // You might need a new method in IQuotaService and QuotaService for this.
        //    // Example: await _quotaService.UpdateQuotaAsync(request.UserId, request.QuotaType, request.Amount);

        //    return Ok(ApiResponse<object>.Success("", "Quota updated successfully."));
        //}
    }
}

// You might need to define a DTO for QuotaUpdateRequest, for example:
// namespace EduVision.Models.DTO.Request
// {
//     public class QuotaUpdateRequest
//     {
//         public int UserId { get; set; }
//         public string QuotaType { get; set; }
//         public int Amount { get; set; }
//     }
// }