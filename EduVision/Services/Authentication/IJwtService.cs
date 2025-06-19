using EduVision.Models;
using EduVision.Models.DTO.Response;

namespace EduVision.Services.Authentication
{
    public interface IJwtService
    {
        TokenResponse GenerateTokens(User user);
        string GenerateRefreshToken();
    }
}
