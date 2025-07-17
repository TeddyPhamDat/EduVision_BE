using EduVision.DBContext;
using EduVision.Models;
using EduVision.Models.DTO.Response;
using EduVision.Models.Entities.Enum;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Linq;

namespace EduVision.Services.Data
{
    public class UserService : IUserService
    {
        private readonly EduVisionContext _context;

        public UserService(EduVisionContext context)
        {
            _context = context;
        }

        public async Task<User> GetUserByIdAsync(int userId)
        {
            return await _context.Users.FindAsync(userId);
        }

        public async Task<User> UpdateUserAsync(User user)
        {
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
            return user;
        }

        public async Task<bool> DeleteUserAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return false;

            // Soft delete: Set user as inactive instead of physically removing
            user.IsActive = false;
            
            // Optionally revoke all refresh tokens for security
            var userTokens = await _context.RefreshTokens
                .Where(rt => rt.UserId == userId)
                .ToListAsync();
            
            foreach (var token in userTokens)
            {
                token.IsRevoked = true;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<PaginatedResponse<AdminUserResponse>> GetUsersAsync(int page, int pageSize, string? search = null, string? role = null)
        {
            var query = _context.Users.AsQueryable();

            // Apply search filter - search by name or email
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(u => 
                    u.FullName.Contains(search) || 
                    u.Email.Contains(search));
            }

            // Apply role filter
            if (!string.IsNullOrEmpty(role) && Enum.TryParse<Role>(role, true, out var roleEnum))
            {
                query = query.Where(u => u.Role == (int)roleEnum);
            }

            var totalCount = await query.CountAsync();
            
            // Order by UserId in descending order (newest first)
            var users = await query
                .OrderByDescending(u => u.UserId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new AdminUserResponse
                {
                    UserId = u.UserId,
                    FullName = u.FullName,
                    Email = u.Email,
                    IsVerified = u.IsVerified,
                    IsActive = u.IsActive,
                    CreatedAt = u.CreatedAt,
                    Role = ((Role)u.Role).ToString()
                })
                .ToListAsync();

            return new PaginatedResponse<AdminUserResponse>
            {
                Data = users,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            };
        }
    }
}