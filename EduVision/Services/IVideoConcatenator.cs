namespace EduVision.Services
{
    public interface IVideoConcatenator
    {
        Task<string> ConcatenateAsync(IEnumerable<string> segmentFiles, string outputPath);
    }
}