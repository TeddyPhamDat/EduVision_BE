namespace EduVision.Models.DTO.Response
{
    public class ContentGenerationStatsResponse
    {
        public ContentTypeStatsResponse Slides { get; set; } = new();
        public ContentTypeStatsResponse Videos { get; set; } = new();
        public List<ContentGenerationTrendResponse> GenerationTrend { get; set; } = new();
        public List<TopUserResponse> TopSlideGenerators { get; set; } = new();
        public List<TopUserResponse> TopVideoGenerators { get; set; } = new();
    }

    public class ContentTypeStatsResponse
    {
        public int Total { get; set; }
        public int Completed { get; set; }
        public int Processing { get; set; }
        public int Failed { get; set; }
        public int GeneratedToday { get; set; }
        public int GeneratedThisWeek { get; set; }
        public int GeneratedThisMonth { get; set; }
    }

    public class ContentGenerationTrendResponse
    {
        public DateTime Date { get; set; }
        public int SlidesGenerated { get; set; }
        public int VideosGenerated { get; set; }
    }

    public class TopUserResponse
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int GeneratedCount { get; set; }
    }
}