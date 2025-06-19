namespace EduVision.Services.Media
{
    public interface IVideoConcatenator
    {
        Task<string> ConcatenateAsync(IEnumerable<string> segmentFiles, string outputPath);
    }
}