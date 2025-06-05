using System.ComponentModel.DataAnnotations;

namespace EduVision.Models.DTO
{
    public class EducationRequestDto
    {
        [Required]
        public string Subject { get; set; } = string.Empty;
        
        [Required]
        public string Chapter { get; set; } = string.Empty;
        
        public int? Grade { get; set; }
        
        public string ImageCategory { get; set; } = string.Empty;
        
        public int Template { get; set; } = 1; // 1=default, 2=dark, 3=modern

        public string Mode { get; set; } = "slides"; // "slides" or "video"
    }
}