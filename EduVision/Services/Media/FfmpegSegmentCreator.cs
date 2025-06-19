using Xabe.FFmpeg;

namespace EduVision.Services.Media
{
    public class FfmpegSegmentCreator : IVideoSegmentCreator
    {
        private readonly ILogger<FfmpegSegmentCreator> _logger;

        public FfmpegSegmentCreator(ILogger<FfmpegSegmentCreator> logger)
        {
            _logger = logger;
        }

        public async Task<string> CreateSegmentAsync(string imagePath, string audioPath, string segmentPath)
        {
            // Example approach using FFmpeg
            var mediaInfo = await FFmpeg.GetMediaInfo(audioPath);
            var audioDuration = mediaInfo.Duration;

            var conversion = FFmpeg.Conversions.New()
                .AddParameter(
                    $"-loop 1 -i \"{imagePath}\" -i \"{audioPath}\" " +
                    $"-c:v libx264 -t {audioDuration.TotalSeconds} -vf \"scale=1920:1080\" " +
                    $"-c:a aac -b:a 192k -shortest -pix_fmt yuv420p \"{segmentPath}\""
                );

            await conversion.Start();

            _logger.LogDebug($"Created segment at: {segmentPath}");
            return segmentPath;
        }
    }
}