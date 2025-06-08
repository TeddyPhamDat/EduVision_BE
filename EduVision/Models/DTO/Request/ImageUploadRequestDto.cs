using System.ComponentModel.DataAnnotations;

namespace EduVision.Models.DTO.Request
{
    public class ImageUploadRequestDto
    {
        public required string Category { get; set; }

        public int? Grade { get; set; }
        public string? Chapter { get; set; }
        public bool HasAllMetadata =>
            !string.IsNullOrWhiteSpace(Category) &&
            Grade.HasValue &&
            !string.IsNullOrWhiteSpace(Chapter);
    }
}