namespace EduVision.Models.DTO.Response
{
    public class LoginResponse
    {
        public string Token { get; set; }
        public string Username { get; set; }

        public string FullName { get; set; }
        public string Email { get; set; }
        public string Role { get; set; }
    }
}
