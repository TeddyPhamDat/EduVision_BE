using EduVision.DBContext;
using EduVision.Models.DTO.Response;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization; // Added for Authorize attribute
using System.Security.Claims; // Added for ClaimTypes

namespace EduVision.Controllers
{
    [Authorize] // Added Authorize attribute for the whole controller
    [ApiController]
    [Route("api/notifications")]
    public class NotificationController : ControllerBase
    {
        private readonly EduVisionContext _context;

        public NotificationController(EduVisionContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Retrieves the notification history for the authenticated user with pagination.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetNotifications(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            var userId = await GetAuthenticatedUserIdAsync();
            if (userId == null)
                return Unauthorized(ApiResponse<string>.Fail("User ID not found in token", 401));

            // Validate pagination
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 10;

            var query = _context.Notifications
                .Where(n => n.UserId == userId.Value); // Use authenticated userId

            var totalCount = await query.CountAsync();
            var notifications = await query
                .OrderByDescending(n => n.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(n => new
                {
                    notificationId = n.NotificationId,
                    userId = n.UserId,
                    message = n.Message,
                    createdAt = n.CreatedAt,
                    isRead = n.IsRead // Include IsRead in the response
                })
                .ToListAsync();

            var paginatedResponse = new PaginatedResponse<object>
            {
                Data = notifications.Cast<object>().ToList(),
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            };

            return Ok(ApiResponse<PaginatedResponse<object>>.Success(paginatedResponse));
        }

        /// <summary>
        /// Retrieves unread notifications for the authenticated user with pagination.
        /// </summary>
        [HttpGet("unread")]
        public async Task<IActionResult> GetUnreadNotifications(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            var userId = await GetAuthenticatedUserIdAsync();
            if (userId == null)
                return Unauthorized(ApiResponse<string>.Fail("User ID not found in token", 401));

            // Validate pagination
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 10;

            var notifications = await _context.Notifications
                .Where(n => n.UserId == userId.Value && !n.IsRead) // Use authenticated userId
                .OrderByDescending(n => n.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(n => new
                {
                    notificationId = n.NotificationId,
                    userId = n.UserId,
                    message = n.Message,
                    createdAt = n.CreatedAt,
                    isRead = n.IsRead
                })
                .ToListAsync();

            var totalCount = await _context.Notifications
                                .Where(n => n.UserId == userId.Value && !n.IsRead)
                                .CountAsync();

            var paginatedResponse = new PaginatedResponse<object>
            {
                Data = notifications.Cast<object>().ToList(),
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            };

            return Ok(ApiResponse<PaginatedResponse<object>>.Success(paginatedResponse));
        }

        /// <summary>
        /// Marks a notification as read for the authenticated user.
        /// </summary>
        [HttpPost("{notificationId}/read")]
        public async Task<IActionResult> MarkAsRead(int notificationId)
        {
            var userId = await GetAuthenticatedUserIdAsync();
            if (userId == null)
                return Unauthorized(ApiResponse<string>.Fail("User ID not found in token", 401));

            var notification = await _context.Notifications
                                    .Where(n => n.NotificationId == notificationId && n.UserId == userId.Value)
                                    .FirstOrDefaultAsync();
            if (notification == null)
                return NotFound(ApiResponse<string>.Fail("Notification not found or does not belong to user", 404));

            notification.IsRead = true;
            await _context.SaveChangesAsync();

            return Ok(ApiResponse<string>.Success("Notification marked as read."));
        }

        /// <summary>
        /// Marks all notifications as read for the authenticated user.
        /// </summary>
        [HttpPost("mark-all-read")]
        public async Task<IActionResult> MarkAllAsRead() // Removed userId from parameter
        {
            var userId = await GetAuthenticatedUserIdAsync();
            if (userId == null)
                return Unauthorized(ApiResponse<string>.Fail("User ID not found in token", 401));

            var notifications = await _context.Notifications
                .Where(n => n.UserId == userId.Value && !n.IsRead) // Use authenticated userId
                .ToListAsync();

            if (notifications.Count == 0)
                return Ok(ApiResponse<string>.Success("No unread notifications."));

            foreach (var notification in notifications)
                notification.IsRead = true;

            await _context.SaveChangesAsync();

            return Ok(ApiResponse<string>.Success("All notifications marked as read."));
        }

        /// <summary>
        /// Deletes a notification by its ID for the authenticated user.
        /// </summary>
        [HttpDelete("{notificationId}")]
        public async Task<IActionResult> DeleteNotification(int notificationId)
        {
            var userId = await GetAuthenticatedUserIdAsync();
            if (userId == null)
                return Unauthorized(ApiResponse<string>.Fail("User ID not found in token", 401));

            var notification = await _context.Notifications
                                    .Where(n => n.NotificationId == notificationId && n.UserId == userId.Value)
                                    .FirstOrDefaultAsync();
            if (notification == null)
                return NotFound(ApiResponse<string>.Fail("Notification not found or does not belong to user", 404));

            _context.Notifications.Remove(notification);
            await _context.SaveChangesAsync();

            return Ok(ApiResponse<string>.Success("Notification deleted."));
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