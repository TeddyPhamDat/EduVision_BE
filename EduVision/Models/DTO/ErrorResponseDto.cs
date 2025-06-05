namespace EduVision.Models.DTO
{
    public class ErrorResponseDto
    {
        public string ErrorCode { get; set; } = "Error";
        public string ErrorMessage { get; set; } = string.Empty;
        public int HttpStatusCode { get; set; }
        public object? Details { get; set; }
    }
}