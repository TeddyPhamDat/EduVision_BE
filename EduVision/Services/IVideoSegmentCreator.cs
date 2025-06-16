namespace EduVision.Services
{
    public interface IVideoSegmentCreator
    {
        Task<string> CreateSegmentAsync(string imagePath, string audioPath, string segmentPath);
    }
}