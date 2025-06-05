namespace EduVision.Models.DTO
{
    public class EducationalItemMetadataDto
    {
        public string Subject { get; set; } = string.Empty;
        public int Grade { get; set; }
        public string Chapter { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public long TotalPages { get; set; }
    }
}