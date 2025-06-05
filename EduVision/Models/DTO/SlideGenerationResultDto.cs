namespace EduVision.Models.DTO
{
    public class SlideGenerationResultDto
    {
        public string? Subject { get; set; }
        public string? Chapter { get; set; }
        public int? Grade { get; set; }
        public string? SlideUrl { get; set; }
        public string? VideoUrl { get; set; }
        public List<LessonSlideDto> Slides { get; set; } = new();
        public string ErrorCode { get; set; } = "Success";
        public string? ErrorMessage { get; set; }
        public int HttpStatusCode { get; set; } = 200;
    }
}