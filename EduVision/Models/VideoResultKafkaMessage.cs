using System;

namespace EduVision.Models
{
    public class VideoResultKafkaMessage
    {
        public int UserId { get; set; }
        public int PromptId { get; set; }
        public string LessonId { get; set; }
        public bool Success { get; set; }
        public string VideoUrl { get; set; }
        public int? DurationSec { get; set; }
        public string Resolution { get; set; }
        public string ErrorMessage { get; set; }
    }
}