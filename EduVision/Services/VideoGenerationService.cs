using Xabe.FFmpeg;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using EduVision.Models.DTO;

namespace EduVision.Services
{
    public class VideoGenerationService
    {
        private readonly AzureBlobStorageService _blobStorage;
        private readonly ILogger<VideoGenerationService> _logger;
        private readonly HttpClient _httpClient;

        public VideoGenerationService(
            AzureBlobStorageService blobStorage,
            ILogger<VideoGenerationService> logger,
            HttpClient httpClient)
        {
            _blobStorage = blobStorage;
            _logger = logger;
            _httpClient = httpClient;
        }

        public async Task<string> GenerateVideoAsync(List<LessonSlideDto> slides, string lessonId)
        {
            if (slides == null || !slides.Any())
                throw new ArgumentException("Slides cannot be null or empty", nameof(slides));

            var tempDir = Path.Combine(Path.GetTempPath(), $"video_gen_{lessonId}_{Guid.NewGuid():N}");

            try
            {
                Directory.CreateDirectory(tempDir);
                _logger.LogInformation($"Created temp directory: {tempDir}");

                var segmentFiles = new List<string>();

                // Download images and audio, create video segments
                for (int i = 0; i < slides.Count; i++)
                {
                    _logger.LogInformation($"Processing slide {i + 1}/{slides.Count}");

                    var slide = slides[i];
                    var imagePath = Path.Combine(tempDir, $"slide{i}.png");
                    var audioPath = Path.Combine(tempDir, $"audio{i}.wav");
                    var segmentPath = Path.Combine(tempDir, $"segment{i}.mp4");

                    // Validate URLs
                    if (string.IsNullOrEmpty(slide.CapturedImageUrl) || string.IsNullOrEmpty(slide.AudioUrl))
                    {
                        _logger.LogWarning($"Slide {i} has missing URLs - Image: {slide.CapturedImageUrl}, Audio: {slide.AudioUrl}");
                        continue;
                    }

                    try
                    {
                        // Download image
                        _logger.LogDebug($"Downloading image from: {slide.CapturedImageUrl}");
                        var imgBytes = await _httpClient.GetByteArrayAsync(slide.CapturedImageUrl);
                        await File.WriteAllBytesAsync(imagePath, imgBytes);

                        // Download audio
                        _logger.LogDebug($"Downloading audio from: {slide.AudioUrl}");
                        var audioBytes = await _httpClient.GetByteArrayAsync(slide.AudioUrl);
                        await File.WriteAllBytesAsync(audioPath, audioBytes);

                        // Verify files exist and have content
                        if (!File.Exists(imagePath) || new FileInfo(imagePath).Length == 0)
                            throw new InvalidOperationException($"Image file is empty or doesn't exist: {imagePath}");

                        if (!File.Exists(audioPath) || new FileInfo(audioPath).Length == 0)
                            throw new InvalidOperationException($"Audio file is empty or doesn't exist: {audioPath}");

                        // Create video segment with Xabe.FFmpeg
                        _logger.LogDebug($"Creating video segment: {segmentPath}");

                        var mediaInfo = await FFmpeg.GetMediaInfo(audioPath);
                        var audioDuration = mediaInfo.Duration;

                        var conversion = FFmpeg.Conversions.New()
                            .AddParameter($"-loop 1 -i \"{imagePath}\" -i \"{audioPath}\" -c:v libx264 -t {audioDuration.TotalSeconds} -vf \"scale=1920:1080\" -c:a aac -b:a 192k -shortest -pix_fmt yuv420p \"{segmentPath}\"");

                        await conversion.Start();

                        if (!File.Exists(segmentPath) || new FileInfo(segmentPath).Length == 0)
                            throw new InvalidOperationException($"Failed to create video segment: {segmentPath}");

                        segmentFiles.Add(segmentPath);
                        _logger.LogDebug($"Successfully created segment: {segmentPath}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error processing slide {i}: {ex.Message}");
                        throw;
                    }
                }

                if (!segmentFiles.Any())
                    throw new InvalidOperationException("No video segments were created");

                // Concatenate segments
                _logger.LogInformation("Concatenating video segments");
                var finalVideoPath = await ConcatenateSegments(segmentFiles, tempDir);

                // Upload to Azure Blob Storage
                _logger.LogInformation("Uploading final video to blob storage");
                using var fs = File.OpenRead(finalVideoPath);
                var videoBlobName = $"presentations/{lessonId}/final_{DateTime.UtcNow:yyyyMMdd_HHmmss}.mp4";
                var videoUrl = await _blobStorage.UploadAsync(videoBlobName, fs, "video/mp4");

                _logger.LogInformation($"Video generation completed successfully. URL: {videoUrl}");
                return videoUrl;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error generating video: {ex.Message}");
                throw;
            }
            finally
            {
                // Cleanup
                try
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, recursive: true);
                        _logger.LogDebug($"Cleaned up temp directory: {tempDir}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to cleanup temp directory {tempDir}: {ex.Message}");
                }
            }
        }

        private async Task<string> ConcatenateSegments(List<string> segmentFiles, string tempDir)
        {
            var finalVideoPath = Path.Combine(tempDir, "final.mp4");

            if (segmentFiles.Count == 1)
            {
                File.Copy(segmentFiles[0], finalVideoPath, overwrite: true);
                return finalVideoPath;
            }

            // Create concat file for FFmpeg
            var concatListPath = Path.Combine(tempDir, "concat.txt");
            var concatContent = segmentFiles.Select(f => $"file '{f.Replace("\\", "/")}'");
            await File.WriteAllLinesAsync(concatListPath, concatContent);

            var conversion = FFmpeg.Conversions.New()
                .AddParameter($"-f concat -safe 0 -i \"{concatListPath}\" -c copy \"{finalVideoPath}\"");

            await conversion.Start();

            if (!File.Exists(finalVideoPath) || new FileInfo(finalVideoPath).Length == 0)
                throw new InvalidOperationException("Failed to create final concatenated video");

            return finalVideoPath;
        }
    }
}