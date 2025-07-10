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
        private readonly IQuotaService _quotaService;
        private readonly IUserService _userService;

        public AdminController(
            EduVisionContext dbContext,
            ILogger<AdminController> logger,
            IQuotaService quotaService,
            IUserService userService)
        {
            _dbContext = dbContext;
            _logger = logger;
            _quotaService = quotaService;
            _userService = userService;
        }

        /// <summary>
        /// Get users with basic filtering and pagination.
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

            var query = _dbContext.Users.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(u => 
                    u.FullName.Contains(search) || 
                    u.Email.Contains(search));
            }

            if (!string.IsNullOrEmpty(role) && Enum.TryParse<Role>(role, true, out var roleEnum))
            {
                query = query.Where(u => u.Role == (int)roleEnum);
            }

            var totalCount = await query.CountAsync();
            var users = await query
                .OrderByDescending(u => u.UserId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new
                {
                    u.UserId,
                    u.FullName,
                    u.Email,
                    u.IsVerified,
                    u.IsActive,
                    u.CreatedAt,
                    Role = ((Role)u.Role).ToString()
                })
                .ToListAsync();

            var response = new PaginatedResponse<object>
            {
                Data = users.Cast<object>().ToList(),
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            };

            return Ok(ApiResponse<PaginatedResponse<object>>.Success(response));
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
        /// Get user status and deletion preview information.
        /// </summary>
        [HttpGet("users/{userId:int}/status")]
        public async Task<IActionResult> GetUserStatus(int userId)
        {
            try
            {
                var user = await _dbContext.Users
                    .Include(u => u.UserQuota)
                    .Include(u => u.Payments)
                    .Include(u => u.Slides)
                    .Include(u => u.GeneratedVideos)
                    .Where(u => u.UserId == userId)
                    .FirstOrDefaultAsync();

                if (user == null)
                    return NotFound(ApiResponse<string>.Fail("User not found", 404));

                var statusInfo = new
                {
                    User = new
                    {
                        user.UserId,
                        user.FullName,
                        user.Email,
                        user.IsActive,
                        user.IsVerified,
                        user.CreatedAt,
                        Role = ((Role)user.Role).ToString()
                    },
                    UserData = new
                    {
                        SlidesCount = user.Slides.Count(),
                        VideosCount = user.GeneratedVideos.Count(),
                        PaymentsCount = user.Payments.Count(),
                        QuotaRecordsCount = user.UserQuota.Count(),
                        TotalPaymentAmount = user.Payments.Where(p => p.Status == "success").Sum(p => p.Amount ?? 0)
                    },
                    CanDeactivate = user.IsActive == true && (Role)user.Role != Role.ADMIN,
                    CanReactivate = user.IsActive == false,
                    Note = user.IsActive == false ? "User is currently deactivated. All data is preserved." : "User is currently active."
                };

                return Ok(ApiResponse<object>.Success(statusInfo));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching user status for userId: {UserId}", userId);
                return StatusCode(500, ApiResponse<string>.Fail($"Failed to fetch user status: {ex.Message}", 500));
            }
        }

        /// <summary>
        /// Get recent payments for monitoring.
        /// </summary>
        [HttpGet("payments")]
        public async Task<IActionResult> GetRecentPayments([FromQuery] int days = 7)
        {
            var payments = await _dbContext.Payments
                .Include(p => p.User)
                .Where(p => p.CreatedAt >= DateTime.UtcNow.AddDays(-days))
                .OrderByDescending(p => p.CreatedAt)
                .Take(50)
                .Select(p => new
                {
                    p.PaymentId,
                    p.OrderCode,
                    p.Amount,
                    p.Status,
                    p.CreatedAt,
                    UserEmail = p.User.Email
                })
                .ToListAsync();

            return Ok(ApiResponse<object>.Success(payments));
        }

        /// <summary>
        /// Simple admin dashboard stats.
        /// </summary>
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard()
        {
            var stats = new
            {
                TotalUsers = await _dbContext.Users.CountAsync(),
                ActiveUsers = await _dbContext.Users.CountAsync(u => u.IsActive == true),
                PendingPayments = await _dbContext.Payments.CountAsync(p => p.Status == "pending"),
                TodaysRevenue = await _dbContext.Payments
                    .Where(p => p.Status == "success" && p.CreatedAt.Value.Date == DateTime.UtcNow.Date)
                    .SumAsync(p => p.Amount ?? 0)
            };

            return Ok(ApiResponse<object>.Success(stats));
        }
    }
}