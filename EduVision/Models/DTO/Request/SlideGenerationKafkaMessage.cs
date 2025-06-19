namespace EduVision.Models.DTO.Request
{
    public class SlideGenerationKafkaMessage
    {
        public int UserId { get; set; }
        public EducationRequestDto Request { get; set; }
    }
}