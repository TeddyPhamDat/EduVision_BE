using System.ComponentModel.DataAnnotations;

namespace EduVision.Models
{
    public class GeminiConfig
    {
        [Required]
        public string? ApiKey { get; set; }
    }
}
