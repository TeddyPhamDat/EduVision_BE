using EduVision.Models;
using System.Threading.Tasks;

namespace EduVision.Services.Data
{
    public interface IUserService
    {
        Task<User> GetUserByIdAsync(int userId);
        Task<User> UpdateUserAsync(User user);
        Task<bool> DeleteUserAsync(int userId);
    }
} 