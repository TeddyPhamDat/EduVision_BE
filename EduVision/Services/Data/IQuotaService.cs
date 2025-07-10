using EduVision.Models.DTO.Response;
using System.Threading.Tasks;

namespace EduVision.Services.Data
{
    public interface IQuotaService
    {
        Task<bool> CheckQuotaAsync(int userId, string quotaType); // quotaType: "slides" hoặc "video"
        Task IncrementQuotaUsedAsync(int userId, string quotaType);
        Task<List<QuotaHistoryResponse>> GetQuotaHistoryAsync(int userId);

        Task IncreaseQuotaAsync(int userId, decimal amount); // amount là số tiền nạp vào
        Task<EduVision.Models.DTO.Response.QuotaSummaryResponse> GetQuotaSummaryAsync(int userId);
        Task UpdateQuotaAsync(int userId, string quotaType, int amount);// amount là số lượng quota mà admin cập nhật
    }
}
