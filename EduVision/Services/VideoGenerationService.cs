using Xabe.FFmpeg;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using EduVision.Models.DTO;

namespace EduVision.Services
{
    // This service orchestrates the creation of video segments from images and audio (via IVideoSegmentCreator),
    // concatenation of those segments (via IVideoConcatenator), and final upload (via IVideoStorageService).
    // Breaking it into separate interfaces allows for easier testing, maintenance, and future development.
    public class VideoGenerationService
    {
        private readonly IVideoSegmentCreator _segmentCreator;
        private readonly IVideoConcatenator _concatenator;
        private readonly IVideoStorageService _storageService;
        private readonly HttpClient _httpClient;
        private readonly ILogger<VideoGenerationService> _logger;

        public VideoGenerationService(
            IVideoSegmentCreator segmentCreator,
            IVideoConcatenator concatenator,
            IVideoStorageService storageService,
            HttpClient httpClient,
            ILogger<VideoGenerationService> logger)
        {
            _segmentCreator = segmentCreator;
            _concatenator = concatenator;
            _storageService = storageService;
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<string> GenerateVideoAsync(List<LessonSlideDto> slides, string lessonId)
        {
            if (slides == null || !slides.Any())
                throw new ArgumentException("Slides cannot be null or empty", nameof(slides));

            var tempDir = Path.Combine(Path.GetTempPath(), $"video_gen_{lessonId}_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            var segmentPaths = new List<string>();

            try
            {
                // 1. Download images/audio and create segments
                for (int i = 0; i < slides.Count; i++)
                {
                    var imagePath = Path.Combine(tempDir, $"slide{i}.png");
                    var audioPath = Path.Combine(tempDir, $"audio{i}.wav");
                    var segmentPath = Path.Combine(tempDir, $"segment{i}.mp4");

                    // Download resources
                    var imgBytes = await _httpClient.GetByteArrayAsync(slides[i].CapturedImageUrl);
                    await File.WriteAllBytesAsync(imagePath, imgBytes);

                    var audioBytes = await _httpClient.GetByteArrayAsync(slides[i].AudioUrl);
                    await File.WriteAllBytesAsync(audioPath, audioBytes);

                    // Create segment
                    await _segmentCreator.CreateSegmentAsync(imagePath, audioPath, segmentPath);
                    segmentPaths.Add(segmentPath);
                }

                if (!segmentPaths.Any())
                    throw new InvalidOperationException("No video segments were created");

                // 2. Concatenate segments
                var finalVideoPath = Path.Combine(tempDir, "final.mp4");
                await _concatenator.ConcatenateAsync(segmentPaths, finalVideoPath);

                // 3. Upload final video
                var uniqueTimestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                var blobName = $"presentations/{lessonId}/final_{uniqueTimestamp}.mp4";
                var videoUrl = await _storageService.UploadAsync(finalVideoPath, blobName, "video/mp4");
                return videoUrl;
            }
            finally
            {
                // Cleanup temp files
                try
                {
                    Directory.Delete(tempDir, recursive: true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Failed cleaning up {tempDir}: {ex}", tempDir, ex.Message);
                }
            }
        }
    }
}