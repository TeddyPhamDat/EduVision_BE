namespace EduVision.Models.DTO.Response
{
    public class VideoResponse
    {
        public int GenerateVideoId { get; set; }
        public string Status { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string VideoUrl { get; set; }
        public string PromptContent { get; set; } // Thêm dòng này
    }
}
