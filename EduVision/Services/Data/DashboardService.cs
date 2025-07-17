using EduVision.DBContext;
using EduVision.Models.DTO.Response;
using EduVision.Models.Entities.Enum;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace EduVision.Services.Data
{
    public class DashboardService : IDashboardService
    {
        private readonly EduVisionContext _context;

        public DashboardService(EduVisionContext context)
        {
            _context = context;
        }

        public async Task<UserStatsResponse> GetUserStatsAsync()
        {
            var totalUsers = await _context.Users.CountAsync();
            var activeUsers = await _context.Users.CountAsync(u => u.IsActive == true);
            var inactiveUsers = await _context.Users.CountAsync(u => u.IsActive == false);
            var verifiedUsers = await _context.Users.CountAsync(u => u.IsVerified == true);
            var unverifiedUsers = await _context.Users.CountAsync(u => u.IsVerified == false);

            // Users by role
            var adminUsers = await _context.Users.CountAsync(u => u.Role == (int)Role.ADMIN);
            var managerUsers = await _context.Users.CountAsync(u => u.Role == (int)Role.MANAGER);
            var regularUsers = await _context.Users.CountAsync(u => u.Role == (int)Role.USER);

            // Registration trend for last 30 days
            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
            var registrationTrend = await _context.Users
                .Where(u => u.CreatedAt >= thirtyDaysAgo)
                .GroupBy(u => u.CreatedAt.HasValue ? u.CreatedAt.Value.Date : DateTime.UtcNow.Date)
                .Select(g => new UserRegistrationTrendResponse
                {
                    Date = g.Key,
                    NewUsers = g.Count()
                })
                .OrderBy(x => x.Date)
                .ToListAsync();

            return new UserStatsResponse
            {
                TotalUsers = totalUsers,
                ActiveUsers = activeUsers,
                InactiveUsers = inactiveUsers,
                VerifiedUsers = verifiedUsers,
                UnverifiedUsers = unverifiedUsers,
                UsersByRole = new UsersByRoleResponse
                {
                    AdminUsers = adminUsers,
                    ManagerUsers = managerUsers,
                    RegularUsers = regularUsers
                },
                RegistrationTrend = registrationTrend
            };
        }

        public async Task<ContentGenerationStatsResponse> GetContentGenerationStatsAsync()
        {
            var today = DateTime.UtcNow.Date;
            var weekAgo = today.AddDays(-7);
            var monthAgo = today.AddDays(-30);

            // Slide statistics
            var totalSlides = await _context.Slides.CountAsync();
            var completedSlides = await _context.Slides.CountAsync(s => s.Status == "Completed");
            var processingSlides = await _context.Slides.CountAsync(s => s.Status == "Processing");
            var failedSlides = await _context.Slides.CountAsync(s => s.Status == "Failed");

            var slidesToday = await _context.Slides
                .Where(s => s.Prompt != null && s.Prompt.CreatedAt.HasValue && s.Prompt.CreatedAt.Value.Date == today)
                .CountAsync();

            var slidesThisWeek = await _context.Slides
                .Where(s => s.Prompt != null && s.Prompt.CreatedAt.HasValue && s.Prompt.CreatedAt.Value.Date >= weekAgo)
                .CountAsync();

            var slidesThisMonth = await _context.Slides
                .Where(s => s.Prompt != null && s.Prompt.CreatedAt.HasValue && s.Prompt.CreatedAt.Value.Date >= monthAgo)
                .CountAsync();

            // Video statistics
            var totalVideos = await _context.GeneratedVideos.CountAsync();
            var completedVideos = await _context.GeneratedVideos.CountAsync(v => v.Status == "Completed");
            var processingVideos = await _context.GeneratedVideos.CountAsync(v => v.Status == "Processing");
            var failedVideos = await _context.GeneratedVideos.CountAsync(v => v.Status == "Failed");

            var videosToday = await _context.GeneratedVideos
                .Where(v => v.CreatedAt.HasValue && v.CreatedAt.Value.Date == today)
                .CountAsync();

            var videosThisWeek = await _context.GeneratedVideos
                .Where(v => v.CreatedAt.HasValue && v.CreatedAt.Value.Date >= weekAgo)
                .CountAsync();

            var videosThisMonth = await _context.GeneratedVideos
                .Where(v => v.CreatedAt.HasValue && v.CreatedAt.Value.Date >= monthAgo)
                .CountAsync();

            // Generation trend for last 30 days
            var generationTrend = await GetGenerationTrendAsync(monthAgo);

            // Top slide generators
            var topSlideGenerators = await _context.Slides
                .Include(s => s.User)
                .Where(s => s.User != null && s.Status == "Completed")
                .GroupBy(s => s.User)
                .Select(g => new TopUserResponse
                {
                    UserId = g.Key.UserId,
                    FullName = g.Key.FullName ?? string.Empty,
                    Email = g.Key.Email ?? string.Empty,
                    GeneratedCount = g.Count()
                })
                .OrderByDescending(x => x.GeneratedCount)
                .Take(10)
                .ToListAsync();

            // Top video generators
            var topVideoGenerators = await _context.GeneratedVideos
                .Include(v => v.User)
                .Where(v => v.User != null && v.Status == "Completed")
                .GroupBy(v => v.User)
                .Select(g => new TopUserResponse
                {
                    UserId = g.Key.UserId,
                    FullName = g.Key.FullName ?? string.Empty,
                    Email = g.Key.Email ?? string.Empty,
                    GeneratedCount = g.Count()
                })
                .OrderByDescending(x => x.GeneratedCount)
                .Take(10)
                .ToListAsync();

            return new ContentGenerationStatsResponse
            {
                Slides = new ContentTypeStatsResponse
                {
                    Total = totalSlides,
                    Completed = completedSlides,
                    Processing = processingSlides,
                    Failed = failedSlides,
                    GeneratedToday = slidesToday,
                    GeneratedThisWeek = slidesThisWeek,
                    GeneratedThisMonth = slidesThisMonth
                },
                Videos = new ContentTypeStatsResponse
                {
                    Total = totalVideos,
                    Completed = completedVideos,
                    Processing = processingVideos,
                    Failed = failedVideos,
                    GeneratedToday = videosToday,
                    GeneratedThisWeek = videosThisWeek,
                    GeneratedThisMonth = videosThisMonth
                },
                GenerationTrend = generationTrend,
                TopSlideGenerators = topSlideGenerators,
                TopVideoGenerators = topVideoGenerators
            };
        }

        private async Task<List<ContentGenerationTrendResponse>> GetGenerationTrendAsync(DateTime startDate)
        {
            var slidesByDate = await _context.Slides
                .Include(s => s.Prompt)
                .Where(s => s.Prompt != null && s.Prompt.CreatedAt.HasValue && s.Prompt.CreatedAt.Value.Date >= startDate)
                .GroupBy(s => s.Prompt.CreatedAt.Value.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .ToListAsync();

            var videosByDate = await _context.GeneratedVideos
                .Where(v => v.CreatedAt.HasValue && v.CreatedAt.Value.Date >= startDate)
                .GroupBy(v => v.CreatedAt.Value.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .ToListAsync();

            var allDates = slidesByDate.Select(x => x.Date)
                .Union(videosByDate.Select(x => x.Date))
                .Distinct()
                .OrderBy(d => d)
                .ToList();

            return allDates.Select(date => new ContentGenerationTrendResponse
            {
                Date = date,
                SlidesGenerated = slidesByDate.FirstOrDefault(s => s.Date == date)?.Count ?? 0,
                VideosGenerated = videosByDate.FirstOrDefault(v => v.Date == date)?.Count ?? 0
            }).ToList();
        }
    }
}