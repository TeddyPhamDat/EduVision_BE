namespace EduVision.Models.DTO.Request
{
    public class AdminUpdateUserRequest
    {
        public string? Role { get; set; } // "USER", "MANAGER", "ADMIN"
        public bool? IsActive { get; set; }
        public bool? IsVerified { get; set; }
    }
}