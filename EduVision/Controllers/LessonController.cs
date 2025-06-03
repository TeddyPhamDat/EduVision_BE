using EduVision.DBContext;
using EduVision.Models;
using EduVision.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace EduVision.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LessonController : ControllerBase
    {
        private readonly GeminiService _geminiService;
        private readonly CloudinaryImageService _imageService;
        private readonly RevealJsGenerator _revealJsGenerator;
        private readonly TextToSpeechService _ttsService;
        private readonly SlideCaptureService _slideCaptureService;
        private readonly VideoGenerationService _videoGenerationService;
        private readonly EduVisionContext _dbContext;

        public LessonController(
            GeminiService geminiService,
            CloudinaryImageService imageService,
            RevealJsGenerator revealJsGenerator,
            TextToSpeechService ttsService,
            SlideCaptureService slideCaptureService,
            EduVisionContext dbContext,
            VideoGenerationService videoGenerationService)
        {
            _geminiService = geminiService;
            _imageService = imageService;
            _revealJsGenerator = revealJsGenerator;
            _ttsService = ttsService;
            _slideCaptureService = slideCaptureService;
            _dbContext = dbContext;
            _videoGenerationService = videoGenerationService;
        }

        [HttpPost("generate")]
        public async Task<IActionResult> GenerateLessonVideo([FromBody] LessonRequest request)
        {
            var logger = HttpContext.RequestServices.GetService<ILogger<LessonController>>();
            var totalSw = Stopwatch.StartNew();

            var sw = Stopwatch.StartNew();
            var imageUrls = await _imageService.GetImagesByCategoryAsync(request.Category, 5);
            sw.Stop();
            logger?.LogInformation("Image fetch: {Elapsed} ms", sw.ElapsedMilliseconds);

            if (imageUrls == null || imageUrls.Count == 0)
                return BadRequest(new { error = $"Category '{request.Category}' is not available or has no images." });

            sw.Restart();
            var slides = await _geminiService.GenerateSlidesAsync(request.Prompt);
            sw.Stop();
            logger?.LogInformation("Gemini slide generation: {Elapsed} ms", sw.ElapsedMilliseconds);

            if (slides == null || slides.Count == 0)
                return BadRequest(new { error = "Failed to generate slides from the prompt." });

            var lessonId = Guid.NewGuid().ToString("N");

            sw.Restart();
            await AssignAssetsToSlidesAsync(slides, imageUrls, lessonId);
            sw.Stop();
            logger?.LogInformation("Assign assets (image/audio): {Elapsed} ms", sw.ElapsedMilliseconds);

            string slideUrl;
            try
            {
                sw.Restart();
                slideUrl = await _revealJsGenerator.GenerateRevealHtmlAsync(slides, lessonId);
                sw.Stop();
                logger?.LogInformation("Reveal.js HTML generation: {Elapsed} ms", sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to generate presentation.", details = ex.Message });
            }

            sw.Restart();
            var capturedImageUrls = await _slideCaptureService.CaptureSlidesAsync(slideUrl, slides.Count, lessonId);
            sw.Stop();
            logger?.LogInformation("Slide image capture: {Elapsed} ms", sw.ElapsedMilliseconds);

            for (int i = 0; i < slides.Count; i++)
                slides[i].CapturedImageUrl = capturedImageUrls[i];

            // After capturing images and audio
            var videoUrl = await _videoGenerationService.GenerateVideoAsync(slides, lessonId);

            using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                // 1. Save Prompt
                var promptEntity = new Prompt
                {
                    UserId = 4, // Use "member" user
                    Content = request.Prompt,
                    CreatedAt = DateTime.UtcNow,
                    Status = "Completed"
                };
                _dbContext.Prompts.Add(promptEntity);
                await _dbContext.SaveChangesAsync();

                // 2. Save Slides
                var slideEntities = slides.Select((slide, i) => new Slide
                {
                    PromptId = promptEntity.Promptid,
                    UserId = 4, // Use "member" user
                    Type = "AI",
                    Url = slide.CapturedImageUrl ?? slide.ImageUrl ?? "",
                    Status = "Completed"
                }).ToList();

                _dbContext.Slides.AddRange(slideEntities);
                await _dbContext.SaveChangesAsync();

                // 3. Save GeneratedVideo
                var videoEntity = new GeneratedVideo
                {
                    PromptId = promptEntity.Promptid,
                    SlideId = slideEntities.FirstOrDefault()?.SlideId,
                    Status = "Completed",
                    CreatedAt = DateTime.UtcNow,
                    VideoUrl = videoUrl,
                };
                _dbContext.GeneratedVideos.Add(videoEntity);
                await _dbContext.SaveChangesAsync();

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }


            totalSw.Stop();
            logger?.LogInformation("Total request time: {Elapsed} ms", totalSw.ElapsedMilliseconds);

            return Ok(new
            {
                SlideUrl = slideUrl,
                Slides = slides,
                VideoUrl = videoUrl
            });
        }

        /// <summary>
        /// Assigns images and generates audio for each slide.
        /// </summary>
        private async Task AssignAssetsToSlidesAsync(List<LessonSlide> slides, List<string> imageUrls, string lessonId)
        {
            var assetTasks = slides.Select((slide, i) => Task.Run(async () =>
            {
                slide.ImageUrl = imageUrls[i % imageUrls.Count];
                var audioBlobName = $"presentations/{lessonId}/audio/{i}.wav";
                slide.AudioUrl = await _ttsService.GenerateAudioAsync(slide.Content, audioBlobName);
            }));
            await Task.WhenAll(assetTasks);
        }
    }
}