using EduVision.DBContext;
using EduVision.Models;
using EduVision.Models.DTO.Request;
using EduVision.Models.DTO.Response;
using EduVision.Models.Entities.Enum;
using EduVision.Services.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduVision.Controllers
{
    /// <summary>
    /// Essential admin operations for user and payment management.
    /// </summary>
    [Authorize(Roles = "ADMIN")]
    [ApiController]
    [Route("api/admin")]
    public class AdminController : ControllerBase
    {
        private readonly EduVisionContext _dbContext;
        private readonly ILogger<AdminController> _logger;
        private readonly IUserService _userService;
        private readonly IDashboardService _dashboardService;

        public AdminController(
            EduVisionContext dbContext,
            ILogger<AdminController> logger,
            IUserService userService,
            IDashboardService dashboardService)
        {
            _dbContext = dbContext;
            _logger = logger;
            _userService = userService;
            _dashboardService = dashboardService;
        }

        /// <summary>
        /// Get all users with option for paging, searching by role or email/FullName.
        /// </summary>
        [HttpGet("users")]
        public async Task<IActionResult> GetUsers(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? search = null,
            [FromQuery] string? role = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            var result = await _userService.GetUsersAsync(page, pageSize, search, role);
            
            return Ok(ApiResponse<PaginatedResponse<AdminUserResponse>>.Success(result));
        }

        /// <summary>
        /// Update user role or status.
        /// </summary>
        [HttpPut("users/{userId:int}")]
        public async Task<IActionResult> UpdateUser(int userId, [FromBody] AdminUpdateUserRequest request)
        {
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null)
                return NotFound(ApiResponse<string>.Fail("User not found", 404));

            // Update role if specified
            if (!string.IsNullOrEmpty(request.Role) && Enum.TryParse<Role>(request.Role, true, out var roleEnum))
            {
                user.Role = (int)roleEnum;
            }

            // Update status if specified
            if (request.IsActive.HasValue)
                user.IsActive = request.IsActive.Value;

            if (request.IsVerified.HasValue)
                user.IsVerified = request.IsVerified.Value;

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Admin updated user {UserId}", userId);
            return Ok(ApiResponse<string>.Success("User updated successfully"));
        }

        /// <summary>
        /// Soft delete a user by setting their status to inactive.
        /// This preserves all user data while preventing access.
        /// </summary>
        [HttpDelete("users/{userId:int}")]
        public async Task<IActionResult> DeleteUser(int userId)
        {
            try
            {
                // Check if user exists
                var user = await _userService.GetUserByIdAsync(userId);
                if (user == null)
                {
                    return NotFound(ApiResponse<string>.Fail("User not found", 404));
                }

                // Check if user is already inactive
                if (user.IsActive == false)
                {
                    return BadRequest(ApiResponse<string>.Fail("User is already inactive", 400));
                }

                // Prevent deletion of admin users (optional safety check)
                if ((Role)user.Role == Role.ADMIN)
                {
                    return BadRequest(ApiResponse<string>.Fail("Cannot deactivate admin users", 400));
                }

                // Log the deletion attempt for audit purposes
                _logger.LogWarning("Admin attempting to deactivate user {UserId} ({Email})", 
                    userId, user.Email);

                // Perform the soft deletion
                var deleted = await _userService.DeleteUserAsync(userId);
                
                if (!deleted)
                {
                    return NotFound(ApiResponse<string>.Fail("User not found or could not be deactivated", 404));
                }

                // Create notification for the user about account deactivation
                var notification = new Notification
                {
                    UserId = userId,
                    Message = "Your account has been deactivated by an administrator. Please contact support for assistance.",
                    CreatedAt = DateTime.UtcNow
                };
                _dbContext.Notifications.Add(notification);
                await _dbContext.SaveChangesAsync();

                // Log successful deletion
                _logger.LogWarning("Admin successfully deactivated user {UserId} ({Email})", 
                    userId, user.Email);

                return Ok(ApiResponse<string>.Success("User has been successfully deactivated"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deactivating user {UserId}", userId);
                return StatusCode(500, ApiResponse<string>.Fail($"Failed to deactivate user: {ex.Message}", 500));
            }
        }

        /// <summary>
        /// Reactivate a previously deactivated user.
        /// </summary>
        [HttpPost("users/{userId:int}/reactivate")]
        public async Task<IActionResult> ReactivateUser(int userId)
        {
            try
            {
                var user = await _userService.GetUserByIdAsync(userId);
                if (user == null)
                {
                    return NotFound(ApiResponse<string>.Fail("User not found", 404));
                }

                if (user.IsActive == true)
                {
                    return BadRequest(ApiResponse<string>.Fail("User is already active", 400));
                }

                // Reactivate user
                user.IsActive = true;
                await _userService.UpdateUserAsync(user);

                // Create notification for successful reactivation
                var notification = new Notification
                {
                    UserId = userId,
                    Message = "Your account has been reactivated by an administrator. Welcome back!",
                    CreatedAt = DateTime.UtcNow
                };
                _dbContext.Notifications.Add(notification);
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("Admin reactivated user {UserId} ({Email}).", 
                    userId, user.Email);

                return Ok(ApiResponse<string>.Success("User has been successfully reactivated"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reactivating user {UserId}", userId);
                return StatusCode(500, ApiResponse<string>.Fail($"Failed to reactivate user: {ex.Message}", 500));
            }
        }

        /// <summary>
        /// Get comprehensive user statistics for admin dashboard.
        /// </summary>
        [HttpGet("dashboard/users")]
        public async Task<IActionResult> GetUserStats()
        {
            var userStats = await _dashboardService.GetUserStatsAsync();
            return Ok(ApiResponse<UserStatsResponse>.Success(userStats));
        }

        /// <summary>
        /// Get comprehensive content generation statistics (videos and slides) for admin dashboard.
        /// </summary>
        [HttpGet("dashboard/content-generation")]
        public async Task<IActionResult> GetContentGenerationStats()
        {
            var contentStats = await _dashboardService.GetContentGenerationStatsAsync();
            return Ok(ApiResponse<ContentGenerationStatsResponse>.Success(contentStats));
        }
    }
}