namespace EduVision.Models
{
    public class LessonRequest
    {
        public required string Prompt { get; set; }  // E.g., "Teach Present Simple"
        public required string Category { get; set; } // E.g., "English"
    }
}
