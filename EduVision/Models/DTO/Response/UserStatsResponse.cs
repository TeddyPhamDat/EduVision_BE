namespace EduVision.Models.DTO.Response
{
    public class UserStatsResponse
    {
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int InactiveUsers { get; set; }
        public int VerifiedUsers { get; set; }
        public int UnverifiedUsers { get; set; }
        public UsersByRoleResponse UsersByRole { get; set; } = new();
        public List<UserRegistrationTrendResponse> RegistrationTrend { get; set; } = new();
    }

    public class UsersByRoleResponse
    {
        public int AdminUsers { get; set; }
        public int ManagerUsers { get; set; }
        public int RegularUsers { get; set; }
    }

    public class UserRegistrationTrendResponse
    {
        public DateTime Date { get; set; }
        public int NewUsers { get; set; }
    }
}