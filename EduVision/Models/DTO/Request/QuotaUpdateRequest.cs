namespace EduVision.Models.DTO.Request
{
    public class QuotaUpdateRequest
    {
        public int UserId { get; set; }
        public string QuotaType { get; set; }
        public int Amount { get; set; }
    }
} 