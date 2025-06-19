namespace EduVision.Models.DTO.Response
{
    public class TokenResponse
    {
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public DateTime AccessTokenExpireAt { get; set; }
    }
}
