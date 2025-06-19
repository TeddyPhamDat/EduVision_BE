using EduVision.DBContext;
using EduVision.Models;
using Microsoft.EntityFrameworkCore;


namespace EduVision.Services.Data
{
    public class QuotaService : IQuotaService
    {
        private readonly EduVisionContext _context;
        public QuotaService(EduVisionContext context)
        {
            _context = context;
        }

        public async Task<bool> CheckQuotaAsync(int userId, string quotaType)
        {
           

            var now = DateTime.UtcNow;
            var quota = await _context.UserQuota
                .FirstOrDefaultAsync(q =>
                    q.UserId == userId &&
                    q.QuotaType == quotaType &&
                    q.PeriodStart.Year == now.Year &&
                    q.PeriodStart.Month == now.Month);


            if (quota == null)
            {
                throw new Exception($"Quota is not configured for user {userId} with type {quotaType}");
            }

            return quota.QuotaUsed < quota.QuotaLimit;
        }

        public async Task IncrementQuotaUsedAsync(int userId, string quotaType)
        {
         

            var now = DateTime.UtcNow;
            var quota = await _context.UserQuota
                .FirstOrDefaultAsync(q =>
                    q.UserId == userId &&
                    q.QuotaType == quotaType &&
                    q.PeriodStart.Year == now.Year &&
                    q.PeriodStart.Month == now.Month);

            if (quota != null)
            {
                quota.QuotaUsed += 1;
                _context.UserQuota.Update(quota);
                await _context.SaveChangesAsync();
            }
        }

        public async Task IncreaseQuotaAsync(int userId, decimal amount)
        {
            int bonusVideo = (int)(amount / 10000);       // 10,000 đ = 1 video
            int bonusSlides = bonusVideo * 5;             // 1 video = 5 slides

            var now = DateTime.UtcNow;
            var currentMonth = new DateTime(now.Year, now.Month, 1);

            // Cập nhật hoặc tạo quota cho "video"
            var videoQuota = await _context.UserQuota
                .FirstOrDefaultAsync(q => q.UserId == userId && q.QuotaType == "video" && q.PeriodStart == currentMonth);

            if (videoQuota == null)
            {
                videoQuota = new UserQuotum
                {
                    UserId = userId,
                    QuotaType = "video",
                    QuotaLimit = 0,
                    QuotaUsed = 0,
                    PeriodStart = currentMonth
                };
                _context.UserQuota.Add(videoQuota);
            }
            videoQuota.QuotaLimit += bonusVideo;

            // Cập nhật hoặc tạo quota cho "slides"
            var slideQuota = await _context.UserQuota
                .FirstOrDefaultAsync(q => q.UserId == userId && q.QuotaType == "slides" && q.PeriodStart == currentMonth);

            if (slideQuota == null)
            {
                slideQuota = new UserQuotum
                {
                    UserId = userId,
                    QuotaType = "slides",
                    QuotaLimit = 0,
                    QuotaUsed = 0,
                    PeriodStart = currentMonth
                };
                _context.UserQuota.Add(slideQuota);
            }
            slideQuota.QuotaLimit += bonusSlides;

            await _context.SaveChangesAsync();
        }

    }
}
