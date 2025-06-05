using System.ComponentModel.DataAnnotations;

namespace EduVision.Models
{
    public class ImageUploadRequest
    {
        [Required]
        public string Category { get; set; }

        public int? Grade { get; set; }
        public string Chapter { get; set; }
        public string Session { get; set; }
        public string Class { get; set; }
        public string Semester { get; set; }
        public int? UserId { get; set; }

        public bool HasAllMetadata =>
            !string.IsNullOrWhiteSpace(Category) &&
            Grade.HasValue &&
            !string.IsNullOrWhiteSpace(Chapter) &&
            !string.IsNullOrWhiteSpace(Session) &&
            !string.IsNullOrWhiteSpace(Class) &&
            !string.IsNullOrWhiteSpace(Semester) &&
            UserId.HasValue;
    }
}