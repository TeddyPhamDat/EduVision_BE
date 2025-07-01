using EduVision.DBContext;
using EduVision.Models;
using System.Threading.Tasks;

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
    }
} 