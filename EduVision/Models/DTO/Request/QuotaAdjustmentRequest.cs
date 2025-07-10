using System.ComponentModel.DataAnnotations;

namespace EduVision.Models.DTO.Request
{
    public class QuotaAdjustmentRequest
    {
        [Required]
        public string QuotaType { get; set; } = string.Empty;
        
        [Range(1, 10000)]
        public int QuotaLimit { get; set; }
    }
}