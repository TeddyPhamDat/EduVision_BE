using EduVision.Models.DTO.Response;
using System.Threading.Tasks;

namespace EduVision.Services.Data
{
    public interface IDashboardService
    {
        Task<UserStatsResponse> GetUserStatsAsync();
        Task<ContentGenerationStatsResponse> GetContentGenerationStatsAsync();
    }
}