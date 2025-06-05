namespace EduVision.Models
{
    public class SlideGenerationResult
    {
        public string ErrorCode { get; set; } = "Success";
        public string? ErrorMessage { get; set; }
        public int HttpStatusCode { get; set; } = 200;
        public List<LessonSlide> Slides { get; set; } = new();
    }
}