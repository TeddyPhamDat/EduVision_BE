using Xabe.FFmpeg;

namespace EduVision.Services.Media
{
    public class FfmpegVideoConcatenator : IVideoConcatenator
    {
        private readonly ILogger<FfmpegVideoConcatenator> _logger;

        public FfmpegVideoConcatenator(ILogger<FfmpegVideoConcatenator> logger)
        {
            _logger = logger;
        }

        public async Task<string> ConcatenateAsync(IEnumerable<string> segmentFiles, string outputPath)
        {
            var segments = segmentFiles.ToList();
            if (!segments.Any())
                throw new InvalidOperationException("No segments found for concatenation.");

            if (segments.Count == 1)
            {
                File.Copy(segments[0], outputPath, overwrite: true);
                return outputPath;
            }

            var tempDir = Path.GetDirectoryName(outputPath) ?? Path.GetTempPath();
            var concatListPath = Path.Combine(tempDir, "concat.txt");
            var concatContent = segments.Select(f => $"file '{f.Replace("\\", "/")}'");
            await File.WriteAllLinesAsync(concatListPath, concatContent);

            var conversion = FFmpeg.Conversions.New()
                .AddParameter($"-f concat -safe 0 -i \"{concatListPath}\" -c copy \"{outputPath}\"");

            await conversion.Start();
            _logger.LogDebug($"Concatenated segments into: {outputPath}");
            return outputPath;
        }
    }
}