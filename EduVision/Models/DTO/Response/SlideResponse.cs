namespace EduVision.Models.DTO.Response
{
    public class SlideResponse
    {
        public int SlideId { get; set; }
        public int? PromptId { get; set; }
        public string Type { get; set; }
        public string Url { get; set; }
        public string Status { get; set; }
        public string PromptContent { get; set; } // Thêm dòng này
    }
}
