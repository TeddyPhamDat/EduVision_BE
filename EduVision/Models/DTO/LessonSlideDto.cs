namespace EduVision.Models.DTO
{
    public class LessonSlideDto
    {
        public string? Title { get; set; }
        public string? Content { get; set; }
        public string? ImageUrl { get; set; }
        public string? AudioUrl { get; set; }
        public string? CapturedImageUrl { get; set; }

        // Add these fields for image matching
        public int? Grade { get; set; }
        public string? Chapter { get; set; }
        public string? Session { get; set; }
        public string? Semester { get; set; }
    }
}
