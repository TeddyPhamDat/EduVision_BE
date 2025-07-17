using EduVision.Models;
using EduVision.Models.DTO.Response;
using System.Threading.Tasks;

namespace EduVision.Services.Data
{
    public interface IUserService
    {
        Task<User> GetUserByIdAsync(int userId);
        Task<User> UpdateUserAsync(User user);
        Task<bool> DeleteUserAsync(int userId);
        Task<PaginatedResponse<AdminUserResponse>> GetUsersAsync(int page, int pageSize, string? search = null, string? role = null);
    }
} 