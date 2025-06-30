using EduVision.DBContext;
using EduVision.Models;
using EduVision.Models.DTO.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.Tasks;
using EduVision.Services.Data;
using EduVision.Models.Entities.Enum;
using EduVision.Models.DTO.Request;

namespace EduVision.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/users")]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;

        public UserController(IUserService userService)
        {
            _userService = userService;
        }

        /// <summary>
        /// Retrieves the personal information of the current authenticated user.
        /// </summary>
        [HttpGet("me")]
        public async Task<IActionResult> GetMe()
        {
            var userIdClaim = User.FindFirst("userId") ?? User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized(ApiResponse<string>.Fail("User ID not found in token", 401));

            var user = await _userService.GetUserByIdAsync(userId);
            if (user == null)
                return NotFound(ApiResponse<string>.Fail("User not found", 404));

           
            var result = new {
                user.UserId,
                user.UserName,
                user.FullName,
                user.Email,
                user.IsVerified,
                user.CreatedAt,
                user.IsActive,
                Role = ((Role)user.Role).ToString(),
                user.PhoneNumber,
                user.AvatarUrl,
                user.Address
            };
            return Ok(ApiResponse<object>.Success(result));
        }

        /// <summary>
        /// Updates the personal information of the current authenticated user (excluding password, email, role, and FCM token).
        /// </summary>
        [HttpPut("me")]
        public async Task<IActionResult> UpdateMe([FromBody] UpdateUserRequest request)
        {
            var userIdClaim = User.FindFirst("userId") ?? User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized(ApiResponse<string>.Fail("User ID not found in token", 401));

            var user = await _userService.GetUserByIdAsync(userId);
            if (user == null)
                return NotFound(ApiResponse<string>.Fail("User not found", 404));

            // Cập nhật thông tin
            user.FullName = request.FullName ?? user.FullName;
            // user.FcmToken = request.FcmToken ?? user.FcmToken;
            user.PhoneNumber = request.PhoneNumber ?? user.PhoneNumber;
            user.AvatarUrl = request.AvatarUrl ?? user.AvatarUrl;
            user.Address = request.Address ?? user.Address;
            await _userService.UpdateUserAsync(user);

            return Ok(ApiResponse<string>.Success("Profile updated successfully"));
        }

    }
} 