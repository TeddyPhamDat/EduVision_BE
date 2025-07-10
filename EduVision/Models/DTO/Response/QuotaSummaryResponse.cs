namespace EduVision.Models.DTO.Response
{
    public class QuotaSummaryResponse
    {
        public int VideoQuotaLimit { get; set; }
        public int VideoQuotaUsed { get; set; }
        public int VideoQuotaRemaining { get; set; }

        public int SlideQuotaLimit { get; set; }
        public int SlideQuotaUsed { get; set; }
        public int SlideQuotaRemaining { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
    }
} 