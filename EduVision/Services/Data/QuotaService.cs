using EduVision.DBContext;
using EduVision.Models;
using EduVision.Models.DTO.Response;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
            var currentMonthStart = new DateTime(now.Year, now.Month, 1);
            var periodEnd = currentMonthStart.AddMonths(1).AddDays(-1);

            // Video quota
            var videoQuota = await _context.UserQuota
                .FirstOrDefaultAsync(q => q.UserId == userId && q.QuotaType == "video" && q.PeriodStart == currentMonthStart);

            if (videoQuota == null)
            {
                videoQuota = new UserQuotum
                {
                    UserId = userId,
                    QuotaType = "video",
                    QuotaLimit = 0,
                    QuotaUsed = 0,
                    PeriodStart = currentMonthStart,
                    PeriodEnd = periodEnd,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                _context.UserQuota.Add(videoQuota);
            }
            videoQuota.QuotaLimit += bonusVideo;
            videoQuota.UpdatedAt = now;

            // Slides quota
            var slideQuota = await _context.UserQuota
                .FirstOrDefaultAsync(q => q.UserId == userId && q.QuotaType == "slides" && q.PeriodStart == currentMonthStart);

            if (slideQuota == null)
            {
                slideQuota = new UserQuotum
                {
                    UserId = userId,
                    QuotaType = "slides",
                    QuotaLimit = 0,
                    QuotaUsed = 0,
                    PeriodStart = currentMonthStart,
                    PeriodEnd = periodEnd,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                _context.UserQuota.Add(slideQuota);
            }
            slideQuota.QuotaLimit += bonusSlides;
            slideQuota.UpdatedAt = now;

            await _context.SaveChangesAsync();
        }

       public async Task<List<QuotaHistoryResponse>> GetQuotaHistoryAsync(int userId)
{
    var history = await _context.UserQuota
        .Where(uq => uq.UserId == userId)
        .Select(uq => new QuotaHistoryResponse
        {
            QuotaType = uq.QuotaType,
            AmountUsed = uq.QuotaUsed,
            QuotaLimit = uq.QuotaLimit,
            PeriodStart = uq.PeriodStart,
            PeriodEnd = uq.PeriodEnd
        })
        .ToListAsync();

    return history;
}
    }
}
