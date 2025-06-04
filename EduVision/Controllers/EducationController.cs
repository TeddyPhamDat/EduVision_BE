using EduVision.Models;
using EduVision.Services;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using EduVision.DBContext;

namespace EduVision.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EducationController : ControllerBase
    {
        private readonly GeminiService _geminiService;
        private readonly CloudinaryImageService _imageService;
        private readonly RevealJsGenerator _revealJsGenerator;
        private readonly TextToSpeechService _ttsService;
        private readonly SlideCaptureService _slideCaptureService;
        private readonly VideoGenerationService _videoGenerationService;
        private readonly MongoDbService _mongoDbService;
        private readonly EduVisionContext _dbContext;
        private readonly ILogger<EducationController> _logger;

        public EducationController(
            GeminiService geminiService,
            CloudinaryImageService imageService,
            RevealJsGenerator revealJsGenerator,
            TextToSpeechService ttsService,
            SlideCaptureService slideCaptureService,
            VideoGenerationService videoGenerationService,
            MongoDbService mongoDbService,
            EduVisionContext dbContext,
            ILogger<EducationController> logger)
        {
            _geminiService = geminiService;
            _imageService = imageService;
            _revealJsGenerator = revealJsGenerator;
            _ttsService = ttsService;
            _slideCaptureService = slideCaptureService;
            _videoGenerationService = videoGenerationService;
            _mongoDbService = mongoDbService;
            _dbContext = dbContext;
            _logger = logger;
        }

        [HttpGet("subjects")]
        public async Task<IActionResult> GetSubjects()
        {
            try
            {
                var subjects = await _mongoDbService.GetAvailableSubjectsAsync();
                return Ok(subjects);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching subjects");
                return StatusCode(500, new { error = "Failed to fetch subjects", details = ex.Message });
            }
        }

        [HttpGet("chapters")]
        public async Task<IActionResult> GetChapters([FromQuery] string subject, [FromQuery] int? grade = null)
        {
            if (string.IsNullOrEmpty(subject))
            {
                return BadRequest(new { error = "Subject parameter is required" });
            }

            try
            {
                var chapters = await _mongoDbService.GetChaptersAsync(subject, grade);
                return Ok(chapters);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching chapters for subject: {Subject}", subject);
                return StatusCode(500, new { error = "Failed to fetch chapters", details = ex.Message });
            }
        }

        [HttpPost("generate")]
        public async Task<IActionResult> GenerateEducationLesson([FromBody] EducationRequest request)
        {
            if (string.IsNullOrEmpty(request.Subject) || string.IsNullOrEmpty(request.Chapter))
            {
                return BadRequest(new { error = "Subject and chapter parameters are required" });
            }

            var totalSw = Stopwatch.StartNew();
            _logger.LogInformation("Starting education lesson generation for subject: {Subject}, chapter: {Chapter}, grade: {Grade}",
                request.Subject, request.Chapter, request.Grade);

            try
            {
                // Use category from request, fallback to "education" if not specified
                string imageCategory = string.IsNullOrEmpty(request.ImageCategory) ? "education" : request.ImageCategory;
                
                // 1. Fetch images
                var sw = Stopwatch.StartNew();
                var imageUrls = await _imageService.GetImagesByCategoryAsync(imageCategory, 5);
                sw.Stop();
                _logger.LogInformation("Image fetch: {Elapsed} ms", sw.ElapsedMilliseconds);

                if (imageUrls == null || imageUrls.Count == 0)
                {
                    return BadRequest(new { error = $"Category '{imageCategory}' is not available or has no images" });
                }

                // 2. Generate slides using MongoDB content as context
                sw.Restart();
                var slides = await _geminiService.GenerateEducationSlidesAsync(request.Subject, request.Chapter, request.Grade);
                sw.Stop();
                _logger.LogInformation("Gemini slide generation with MongoDB context: {Elapsed} ms", sw.ElapsedMilliseconds);

                if (slides == null || slides.Count == 0)
                {
                    return BadRequest(new { error = "Failed to generate slides" });
                }

                // 3. Generate unique ID for this lesson
                var lessonId = Guid.NewGuid().ToString("N");

                // 4. Assign images and generate audio for slides
                sw.Restart();
                await AssignAssetsToSlidesAsync(slides, imageUrls, lessonId);
                sw.Stop();
                _logger.LogInformation("Assign assets (image/audio): {Elapsed} ms", sw.ElapsedMilliseconds);

                // 5. Generate HTML presentation
                string slideUrl;
                try
                {
                    sw.Restart();
                    slideUrl = await _revealJsGenerator.GenerateRevealHtmlAsync(slides, lessonId);
                    sw.Stop();
                    _logger.LogInformation("Reveal.js HTML generation: {Elapsed} ms", sw.ElapsedMilliseconds);
                }
                catch (Exception ex)
                {
                    return StatusCode(500, new { error = "Failed to generate presentation", details = ex.Message });
                }

                // 6. Capture slide images
                sw.Restart();
                var capturedImageUrls = await _slideCaptureService.CaptureSlidesAsync(slideUrl, slides.Count, lessonId);
                sw.Stop();
                _logger.LogInformation("Slide image capture: {Elapsed} ms", sw.ElapsedMilliseconds);

                for (int i = 0; i < slides.Count; i++)
                {
                    slides[i].CapturedImageUrl = capturedImageUrls[i];
                }

                // 7. Generate video
                sw.Restart();
                var videoUrl = await _videoGenerationService.GenerateVideoAsync(slides, lessonId);
                sw.Stop();
                _logger.LogInformation("Video generation: {Elapsed} ms", sw.ElapsedMilliseconds);

                // 8. Save to database
                using var transaction = await _dbContext.Database.BeginTransactionAsync();
                try
                {
                    // Save Prompt
                    var promptEntity = new Prompt
                    {
                        UserId = 3, // Use "member" user
                        Content = $"{request.Subject} - {request.Chapter} - Grade {request.Grade}",
                        CreatedAt = DateTime.UtcNow,
                        Status = "Completed"
                    };
                    _dbContext.Prompts.Add(promptEntity);
                    await _dbContext.SaveChangesAsync();

                    // Save Slides
                    var slideEntities = slides.Select((slide, i) => new Slide
                    {
                        PromptId = promptEntity.Promptid,
                        UserId = 3, // Use "member" user
                        Type = request.Subject,
                        Url = slide.CapturedImageUrl ?? slide.ImageUrl ?? "",
                        Status = "Completed"
                    }).ToList();

                    _dbContext.Slides.AddRange(slideEntities);
                    await _dbContext.SaveChangesAsync();

                    // Save GeneratedVideo
                    var videoEntity = new GeneratedVideo
                    {
                        PromptId = promptEntity.Promptid,
                        SlideId = slideEntities.FirstOrDefault()?.SlideId,
                        Status = "Completed",
                        CreatedAt = DateTime.UtcNow,
                        VideoUrl = videoUrl
                    };
                    _dbContext.GeneratedVideos.Add(videoEntity);
                    await _dbContext.SaveChangesAsync();

                    await transaction.CommitAsync();
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Database error while saving lesson");
                    return StatusCode(500, new { error = "Failed to save lesson data", details = ex.Message });
                }

                totalSw.Stop();
                _logger.LogInformation("Total request time: {Elapsed} ms", totalSw.ElapsedMilliseconds);

                return Ok(new
                {
                    Subject = request.Subject,
                    Chapter = request.Chapter,
                    Grade = request.Grade,
                    SlideUrl = slideUrl,
                    Slides = slides,
                    VideoUrl = videoUrl
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating education lesson");
                return StatusCode(500, new { error = "Failed to generate education lesson", details = ex.Message });
            }
        }

        private async Task AssignAssetsToSlidesAsync(List<LessonSlide> slides, List<string> imageUrls, string lessonId)
        {
            var assetTasks = slides.Select((slide, i) => Task.Run(async () =>
            {
                slide.ImageUrl = imageUrls[i % imageUrls.Count];
                var audioBlobName = $"presentations/{lessonId}/audio/{i}.wav";
                slide.AudioUrl = await _ttsService.GenerateAudioAsync(slide.Content ?? string.Empty, audioBlobName);
            }));
            await Task.WhenAll(assetTasks);
        }
    }
}