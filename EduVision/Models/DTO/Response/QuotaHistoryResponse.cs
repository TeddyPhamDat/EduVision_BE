namespace EduVision.Models.DTO.Response
{
    public class QuotaHistoryResponse
    {
        public string QuotaType { get; set; } // Loại quota (slides, video, v.v.)
        public decimal AmountUsed { get; set; } // Số lượng quota đã sử dụng
        public decimal QuotaLimit { get; set; } // Giới hạn quota hiện tại
        public DateTime PeriodStart { get; set; } // Thời gian sử dụng

        public DateTime PeriodEnd { get; set; } // Thời gian kết thúc của giai đoạn quota


    }
} 