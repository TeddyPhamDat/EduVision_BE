using EduVision.DBContext;
using EduVision.Models.DTO.Response;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EduVision.Controllers
{
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
        /// Retrieves the notification history for a specific user.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetNotifications([FromQuery] int userId)
        {
            var notifications = await _context.Notifications
                .Include(n => n.User)
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Select(n => new
                {
                    notificationId = n.NotificationId,
                    userId = n.UserId,
                    message = n.Message,
                    createdAt = n.CreatedAt,
                    user = new
                    {
                        userId = n.User.UserId,
                    }
                })
                .ToListAsync();

            return Ok(ApiResponse<object>.Success(notifications));
        }
    }
} 