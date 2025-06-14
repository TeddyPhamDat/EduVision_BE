namespace EduVision.Services
{
    public interface IQuotaService
    {
        Task<bool> CheckQuotaAsync(int userId, string quotaType); // quotaType: "slides" hoặc "video"
        Task IncrementQuotaUsedAsync(int userId, string quotaType);

        Task IncreaseQuotaAsync(int userId, decimal amount); // amount là số tiền nạp vào
      
    }
}
