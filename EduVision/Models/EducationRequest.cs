using System.ComponentModel.DataAnnotations;

namespace EduVision.Models
{
    public class EducationRequest
    {
        [Required]
        public string Subject { get; set; } = string.Empty;
        
        [Required]
        public string Chapter { get; set; } = string.Empty;
        
        public int? Grade { get; set; }
        
        public string ImageCategory { get; set; } = "education";
    }
}