using EduVision.Models;
using EduVision.Services;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using EduVision.DBContext;
using EduVision.Models.DTO;
using Microsoft.EntityFrameworkCore;

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
        [ProducesResponseType(typeof(SlideGenerationResultDto), 200)]
        [ProducesResponseType(typeof(ErrorResponseDto), 400)]
        [ProducesResponseType(typeof(ErrorResponseDto), 500)]
        public async Task<ActionResult<SlideGenerationResultDto>> GenerateEducationLesson([FromBody] EducationRequestDto request)
        {
            if (string.IsNullOrEmpty(request.Subject) || string.IsNullOrEmpty(request.Chapter))
            {
                return BadRequest(new ErrorResponseDto
                {
                    ErrorCode = "MissingParameter",
                    ErrorMessage = "Subject and chapter parameters are required",
                    HttpStatusCode = 400
                });
            }

            var totalSw = Stopwatch.StartNew();
            _logger.LogInformation("Starting education lesson generation for subject: {Subject}, chapter: {Chapter}, grade: {Grade}",
                request.Subject, request.Chapter, request.Grade);

            try
            {
                string imageCategory = string.IsNullOrEmpty(request.ImageCategory) ? "education" : request.ImageCategory;

                var sw = Stopwatch.StartNew();
                var slideResult = await _geminiService.GenerateEducationSlidesAsync(request.Subject, request.Chapter, request.Grade);

                if (slideResult == null || slideResult.HttpStatusCode != 200)
                {
                    return StatusCode(
                        slideResult?.HttpStatusCode ?? 500,
                        new ErrorResponseDto
                        {
                            ErrorCode = slideResult?.ErrorCode ?? "SlideGenerationFailed",
                            ErrorMessage = slideResult?.ErrorMessage ?? "Failed to generate slides",
                            HttpStatusCode = slideResult?.HttpStatusCode ?? 500
                        }
                    );
                }

                var slides = slideResult.Slides;
                if (slides == null || slides.Count == 0)
                {
                    return BadRequest(new ErrorResponseDto
                    {
                        ErrorCode = "NoSlides",
                        ErrorMessage = "No slides generated",
                        HttpStatusCode = 400
                    });
                }

                var imageUrls = await GetBestImagesForSlidesAsync(imageCategory, request.Grade, request.Chapter, slides.Count);

                if (imageUrls == null || imageUrls.All(string.IsNullOrEmpty))
                {
                    return BadRequest(new ErrorResponseDto
                    {
                        ErrorCode = "NoImages",
                        ErrorMessage = $"No images found for category '{imageCategory}' (specific or default) during slide generation.",
                        HttpStatusCode = 400
                    });
                }

                for (int i = 0; i < slides.Count; i++)
                {
                    slides[i].ImageUrl = imageUrls[i];
                }

                sw.Stop();
                _logger.LogInformation("Image fetch and assignment: {Elapsed} ms", sw.ElapsedMilliseconds);

                var lessonId = Guid.NewGuid().ToString("N");

                string slideUrl;
                try
                {
                    sw.Restart();
                    int template = request.Template;
                    string templateName = template switch
                    {
                        2 => "RevealTemplateDark.html",
                        3 => "RevealTemplateModern.html",
                        1 => "RevealTemplate.html",
                        _ => "RevealTemplateDark.html"
                    };
                    slideUrl = await _revealJsGenerator.GenerateRevealHtmlAsync(slides, lessonId, templateName);
                    sw.Stop();
                    _logger.LogInformation("Reveal.js HTML generation: {Elapsed} ms", sw.ElapsedMilliseconds);
                }
                catch (Exception ex)
                {
                    return StatusCode(500, new ErrorResponseDto
                    {
                        ErrorCode = "PresentationGenerationFailed",
                        ErrorMessage = "Failed to generate presentation",
                        HttpStatusCode = 500,
                        Details = ex.Message
                    });
                }

                if (request.Mode == "slides")
                {
                    return Ok(new SlideGenerationResultDto
                    {
                        Subject = request.Subject,
                        Chapter = request.Chapter,
                        Grade = request.Grade,
                        SlideUrl = slideUrl,
                        Slides = slides,
                    });
                }

                await AssignAudioToSlidesAsync(slides, lessonId);

                sw.Restart();
                var capturedImageUrls = await _slideCaptureService.CaptureSlidesAsync(slideUrl, slides.Count, lessonId);
                sw.Stop();
                _logger.LogInformation("Slide image capture: {Elapsed} ms", sw.ElapsedMilliseconds);

                for (int i = 0; i < slides.Count; i++)
                {
                    slides[i].CapturedImageUrl = capturedImageUrls[i];
                }

                sw.Restart();
                var videoUrl = await _videoGenerationService.GenerateVideoAsync(slides, lessonId);
                sw.Stop();
                _logger.LogInformation("Video generation: {Elapsed} ms", sw.ElapsedMilliseconds);

                using var transaction = await _dbContext.Database.BeginTransactionAsync();
                try
                {
                    var promptEntity = new Prompt
                    {
                        UserId = 3,
                        Content = $"{request.Subject} - {request.Chapter} - Grade {request.Grade} - Template {request.Template} - {request.Mode}",
                        CreatedAt = DateTime.UtcNow,
                        Status = "Completed"
                    };
                    _dbContext.Prompts.Add(promptEntity);
                    await _dbContext.SaveChangesAsync();

                    var slideEntities = slides.Select((slide, i) => new Slide
                    {
                        PromptId = promptEntity.Promptid,
                        UserId = 3,
                        Type = request.Subject,
                        Url = slide.CapturedImageUrl ?? slide.ImageUrl ?? "",
                        Status = "Completed"
                    }).ToList();

                    _dbContext.Slides.AddRange(slideEntities);
                    await _dbContext.SaveChangesAsync();

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
                    return StatusCode(500, new ErrorResponseDto
                    {
                        ErrorCode = "DatabaseError",
                        ErrorMessage = "Failed to save lesson data",
                        HttpStatusCode = 500,
                        Details = ex.Message
                    });
                }

                totalSw.Stop();
                _logger.LogInformation("Total request time: {Elapsed} ms", totalSw.ElapsedMilliseconds);

                return Ok(new SlideGenerationResultDto
                {
                    Subject = request.Subject,
                    Chapter = request.Chapter,
                    Grade = request.Grade,
                    SlideUrl = slideUrl,
                    VideoUrl = videoUrl,
                    Slides = slides,
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating education lesson");
                return StatusCode(500, new ErrorResponseDto
                {
                    ErrorCode = "UnhandledException",
                    ErrorMessage = "Failed to generate education lesson",
                    HttpStatusCode = 500,
                    Details = ex.Message
                });
            }
        }

        private async Task AssignAudioToSlidesAsync(List<LessonSlideDto> slides, string lessonId)
        {
            var audioTasks = slides.Select((slide, i) => Task.Run(async () =>
            {
                var audioBlobName = $"presentations/{lessonId}/audio/{i}.wav";
                slide.AudioUrl = await _ttsService.GenerateAudioAsync(slide.Content ?? string.Empty, audioBlobName);
            }));
            await Task.WhenAll(audioTasks);
        }

        // Helper method to assign each specific image only once, then use defaults (randomized), with error handling
        private async Task<List<string>?> GetBestImagesForSlidesAsync(
            string category, int? grade, string chapter, int slideCount)
        {
            string gradeStr = grade.HasValue ? $"Grade{grade}" : "None";
            string chapterStr = !string.IsNullOrWhiteSpace(chapter) ? chapter : "None";

            // Fetch all relevant images in one query
            var images = await _dbContext.Images
                .Where(i => i.Category == category && i.Status == "Active" &&
                    (
                        (i.Grade == gradeStr && i.Chapter == chapterStr) // specific
                        || (i.Grade == "None" && i.Chapter == "None")    // default
                    ))
                .ToListAsync();

            var rng = new Random();
            var specificImages = images
                .Where(i => i.Grade == gradeStr && i.Chapter == chapterStr)
                .Select(i => i.Url)
                .OrderBy(_ => rng.Next())
                .ToList();

            var defaultImages = images
                .Where(i => i.Grade == "None" && i.Chapter == "None")
                .Select(i => i.Url)
                .OrderBy(_ => rng.Next())
                .ToList();

            // If there are no images at all, return null to signal error
            if (specificImages.Count == 0 && defaultImages.Count == 0)
                return null;

            var result = new List<string>();

            // Assign each specific image only once, in random order
            foreach (var url in specificImages)
            {
                if (result.Count < slideCount)
                    result.Add(url);
                else
                    break;
            }

            // Fill the rest with default images (cycled, but shuffled)
            int defaultIndex = 0;
            while (result.Count < slideCount)
            {
                if (defaultImages.Count > 0)
                {
                    result.Add(defaultImages[defaultIndex % defaultImages.Count]);
                    defaultIndex++;
                }
                else
                {
                    result.Add(string.Empty); // fallback if no images at all
                }
            }

            return result;
        }
    }
}