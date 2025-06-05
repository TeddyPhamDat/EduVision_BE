namespace EduVision.Models.DTO
{
    public class SlideGenerationResultDto
    {
        public string ErrorCode { get; set; } = "Success";
        public string? ErrorMessage { get; set; }
        public int HttpStatusCode { get; set; } = 200;
        public List<LessonSlideDto> Slides { get; set; } = new();
    }
}