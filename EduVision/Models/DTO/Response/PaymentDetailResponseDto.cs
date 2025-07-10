namespace EduVision.Models.DTO.Response
{
    public class PaymentDetailResponseDto
    {
        public int PaymentId { get; set; }
        public int? UserId { get; set; }
        public decimal? Amount { get; set; }
        public string Status { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string OrderCode { get; set; }
    }
} 