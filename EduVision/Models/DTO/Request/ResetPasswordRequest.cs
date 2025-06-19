namespace EduVision.Models.DTO.Request
{
    public class ResetPasswordRequest
    {
        public string Email { get; set; }
        public string OtpToken { get; set; }
        public string NewPassword { get; set; }
    }
}
