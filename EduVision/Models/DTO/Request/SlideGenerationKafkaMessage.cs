namespace EduVision.Models.DTO.Request
{
    public class SlideGenerationKafkaMessage
    {
        public int UserId { get; set; }
        public EducationRequestDto Request { get; set; }
        // flag to indicate if video generation is requested
        public bool GenerateVideo { get; set; } = false;
        public int PromptId { get; set; }
    }
}