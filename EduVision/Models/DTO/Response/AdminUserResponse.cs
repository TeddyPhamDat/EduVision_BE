namespace EduVision.Models.DTO.Response
{
    public class AdminUserResponse
    {
        public int UserId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public bool? IsVerified { get; set; }
        public bool? IsActive { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string Role { get; set; }
    }
}