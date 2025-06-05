using System.ComponentModel.DataAnnotations;

namespace EduVision.Models.Config
{
    public class GeminiConfig
    {
        [Required]
        public string? ApiKey { get; set; }
    }
}
