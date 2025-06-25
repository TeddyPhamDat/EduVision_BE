using EduVision.Models.DTO;
using System.Collections.Generic;

namespace EduVision.Models
{
    public class VideoGenerationKafkaMessage
    {
        public int UserId { get; set; }
        public int PromptId { get; set; }
        public string LessonId { get; set; }
        public string SlideUrl { get; set; }
        public List<LessonSlideDto> Slides { get; set; } = new();
    }
}