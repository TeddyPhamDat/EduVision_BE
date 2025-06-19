namespace EduVision.Services.Media
{
    public interface IVideoSegmentCreator
    {
        Task<string> CreateSegmentAsync(string imagePath, string audioPath, string segmentPath);
    }
}