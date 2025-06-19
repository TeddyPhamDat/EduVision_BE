using EduVision.DBContext;
using EduVision.Models;
using EduVision.Models.DTO;
using EduVision.Models.DTO.Request;
using EduVision.Models.DTO.Response;
using EduVision.Models.Entities.Enum;
using EduVision.Services.AI;
using EduVision.Services.Data;
using EduVision.Services.Media;
using EduVision.Services.Presentation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Security.Claims;

namespace EduVision.Controllers
{
    // The EducationController provides endpoints for generating educational content.
    // The API is split into two main endpoints: one for slide-only generation and one for full video lessons.
    // This separation is necessary to clearly distinguish between use cases and to simplify client integration.
    [ApiController]
    [Route("api/education")]
    public class EducationController : ControllerBase
    {
        // Dependencies are injected to follow the Dependency Injection principle.
        // This makes the controller easier to test and maintain, and allows for flexible service replacement.
        private readonly IGeminiService _geminiService;
        private readonly RevealJsGenerator _revealJsGenerator;
        private readonly TextToSpeechService _ttsService;
        private readonly SlideCaptureService _slideCaptureService;
        private readonly VideoGenerationService _videoGenerationService;
        private readonly IMongoDbService _mongoDbService;
        private readonly EduVisionContext _dbContext;
        private readonly ILogger<EducationController> _logger;
        private readonly IQuotaService _quotaService;
        private readonly KafkaProducerService _kafkaProducerService;
        private readonly SlideImageSelectorService _slideImageSelectorService;

        // Constructor injects all required services for lesson generation, storage, and quota management.
        // This ensures all business logic is handled by dedicated services, keeping the controller thin.
        public EducationController(
            IGeminiService geminiService,
            RevealJsGenerator revealJsGenerator,
            TextToSpeechService ttsService,
            SlideCaptureService slideCaptureService,
            VideoGenerationService videoGenerationService,
            IMongoDbService mongoDbService,
            EduVisionContext dbContext,
            ILogger<EducationController> logger,
            IQuotaService quotaService,
            KafkaProducerService kafkaProducerService,
            SlideImageSelectorService slideImageSelectorService)
        {
            _geminiService = geminiService;
            _revealJsGenerator = revealJsGenerator;
            _ttsService = ttsService;
            _slideCaptureService = slideCaptureService;
            _videoGenerationService = videoGenerationService;
            _mongoDbService = mongoDbService;
            _dbContext = dbContext;
            _logger = logger;
            _quotaService = quotaService;
            _kafkaProducerService = kafkaProducerService;
            _slideImageSelectorService = slideImageSelectorService;
        }

        /// <summary>
        /// Get all available subjects for lesson generation.
        /// </summary>
        // Why: Allows clients to dynamically display subject options to users, ensuring up-to-date and relevant content selection.
        [HttpGet("subjects")]
        public async Task<IActionResult> GetSubjects()
        {
            try
            {
                var subjects = await _mongoDbService.GetAvailableSubjectsAsync();
                return Ok(ApiResponse<List<string>>.Success(subjects));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching subjects");
                return StatusCode(500, ApiResponse<string>.Fail($"Failed to fetch subjects: {ex.Message}", 500));
            }
        }

        /// <summary>
        /// Get all chapters for a given subject and grade.
        /// </summary>
        // Why: Enables clients to present chapter options based on the selected subject and grade, supporting lesson customization and navigation.
        [HttpGet("chapters")]
        public async Task<IActionResult> GetChapters([FromQuery] string subject, [FromQuery] int? grade = null)
        {
            if (string.IsNullOrEmpty(subject))
            {
                return BadRequest(ApiResponse<string>.Fail("Subject parameter is required", 400));
            }

            try
            {
                var chapters = await _mongoDbService.GetChaptersAsync(subject, grade);
                return Ok(ApiResponse<List<string>>.Success(chapters));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching chapters for subject: {Subject}", subject);
                return StatusCode(500, ApiResponse<string>.Fail($"Failed to fetch chapters: {ex.Message}", 500));
            }
        }

        /// <summary>
        /// Generate slides for a lesson.
        /// </summary>
        // Why: This endpoint is for users who only need visual learning materials, not full video lessons.
        [Authorize(Roles = "USER,MANAGER,ADMIN")]
        [HttpPost("slides")]
        [ProducesResponseType(typeof(SlideGenerationResultDto), 202)]
        public async Task<ActionResult<SlideGenerationResultDto>> CreateSlides([FromBody] EducationRequestDto request)
        {
            var userIdClaim = User.FindFirst("userId") ?? User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized(ApiResponse<string>.Fail("User ID not found in token", 401));

            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null)
                return BadRequest(ApiResponse<string>.Fail("User does not exist", 404));
            if ((Role)user.Role != Role.MANAGER)
            {
                bool hasQuota = await _quotaService.CheckQuotaAsync(userId, "slides");
                if (!hasQuota)
                    return BadRequest(ApiResponse<string>.Fail("You have exceeded the number of slide generations for the month", 400));
            }

            if (string.IsNullOrEmpty(request.Subject) || string.IsNullOrEmpty(request.Chapter))
                return BadRequest(ApiResponse<string>.Fail("Subject and chapter parameters are required", 400));

            // Enqueue the job to Kafka
            await _kafkaProducerService.ProduceAsync(new SlideGenerationKafkaMessage
            {
                UserId = userId,
                Request = request
            });

            // Return 202 Accepted
            return Accepted(ApiResponse<string>.Success("Slide generation request accepted and is being processed."));
        }

        /// <summary>
        /// Get all slides created by the authenticated user.
        /// </summary>
        [Authorize]
        [HttpGet("slides")]
        public async Task<IActionResult> GetMySlides()
        {
            var userIdClaim = User.FindFirst("userId") ?? User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized(ApiResponse<string>.Fail("User ID not found in token", 401));

            var slides = await _dbContext.Slides
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.SlideId)
                .ToListAsync();

            return Ok(ApiResponse<List<Slide>>.Success(slides));
        }

        /// <summary>
        /// Generate a full video lesson (with audio).
        /// </summary>
        // Why: This endpoint is for users who want a complete multimedia lesson, including narration and video composition.
        [Authorize(Roles = "USER")]
        [HttpPost("videos")]
        [ProducesResponseType(typeof(SlideGenerationResultDto), 200)]
        public async Task<ActionResult<SlideGenerationResultDto>> CreateVideoLesson([FromBody] EducationRequestDto request)
        {
            // Extract user ID from JWT claims for quota and ownership checks.
            var userIdClaim = User.FindFirst("userId") ?? User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized(ApiResponse<string>.Fail("User ID not found in token", 401));
            }

            // Enforce video quota for non-manager users to prevent abuse and manage system resources.
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null)
                return BadRequest(ApiResponse<string>.Fail("User does not exist", 404));
            if ((Role)user.Role != Role.MANAGER)
            {
                bool hasQuota = await _quotaService.CheckQuotaAsync(userId, "video");
                if (!hasQuota)
                    return BadRequest(ApiResponse<string>.Fail("You have exceeded the number of video generations for the month", 400));
            }

            // Validate required parameters to ensure the request is meaningful.
            if (string.IsNullOrEmpty(request.Subject) || string.IsNullOrEmpty(request.Chapter))
                return BadRequest(ApiResponse<string>.Fail("Subject and chapter parameters are required", 400));

            var totalSw = Stopwatch.StartNew();
            _logger.LogInformation("Starting video lesson generation for subject: {Subject}, chapter: {Chapter}, grade: {Grade}",
                request.Subject, request.Chapter, request.Grade);

            try
            {
                // Use a default image category if not specified to ensure every slide has an image.
                string imageCategory = string.IsNullOrEmpty(request.ImageCategory) ? "education" : request.ImageCategory;
                var sw = Stopwatch.StartNew();

                // Generate slide content using Gemini AI service.
                var slideResult = await _geminiService.GenerateEducationSlidesAsync(request.Subject, request.Chapter, request.Grade);

                if (slideResult == null || slideResult.HttpStatusCode != 200)
                {
                    return StatusCode(
                        slideResult?.HttpStatusCode ?? 500,
                        ApiResponse<string>.Fail(slideResult?.ErrorMessage ?? "Failed to generate slides", slideResult?.HttpStatusCode ?? 500)
                    );
                }

                var slides = slideResult.Slides;
                if (slides == null || slides.Count == 0)
                    return BadRequest(ApiResponse<string>.Fail("No slides generated", 400));

                // Assign images to each slide to enhance engagement and educational value.
                var imageUrls = await _slideImageSelectorService.GetBestImagesForSlidesAsync(imageCategory, request.Grade, request.Chapter, slides.Count);
                if (imageUrls == null || imageUrls.All(string.IsNullOrEmpty))
                    return BadRequest(ApiResponse<string>.Fail($"No images found for category '{imageCategory}'", 400));

                for (int i = 0; i < slides.Count; i++)
                    slides[i].ImageUrl = imageUrls[i];

                sw.Stop();
                _logger.LogInformation("Image fetch and assignment: {Elapsed} ms", sw.ElapsedMilliseconds);

                // Generate a unique lesson ID for storage and retrieval.
                var lessonId = Guid.NewGuid().ToString("N");
                string slideUrl;
                try
                {
                    sw.Restart();
                    // Select the appropriate Reveal.js template based on user preference.
                    int template = request.Template;
                    string templateName = template switch
                    {
                        2 => "RevealTemplateDark.html",
                        3 => "RevealTemplateModern.html",
                        1 => "RevealTemplate.html",
                        _ => "RevealTemplateDark.html"
                    };
                    // Generate the HTML presentation and upload it to storage.
                    slideUrl = await _revealJsGenerator.GenerateRevealHtmlAsync(slides, lessonId, templateName);
                    sw.Stop();
                    _logger.LogInformation("Reveal.js HTML generation: {Elapsed} ms", sw.ElapsedMilliseconds);
                }
                catch (Exception ex)
                {
                    return StatusCode(500, ApiResponse<string>.Fail("Failed to generate presentation: " + ex.Message, 500));
                }

                // Generate audio narration for each slide using TTS.
                await AssignAudioToSlidesAsync(slides, lessonId);

                // Capture slide images for video composition.
                sw.Restart();
                var capturedImageUrls = await _slideCaptureService.CaptureSlidesAsync(slideUrl, slides.Count, lessonId);
                sw.Stop();
                _logger.LogInformation("Slide image capture: {Elapsed} ms", sw.ElapsedMilliseconds);

                for (int i = 0; i < slides.Count; i++)
                    slides[i].CapturedImageUrl = capturedImageUrls[i];

                // Generate the final video by combining images and audio.
                sw.Restart();
                var videoUrl = await _videoGenerationService.GenerateVideoAsync(slides, lessonId);
                sw.Stop();
                _logger.LogInformation("Video generation: {Elapsed} ms", sw.ElapsedMilliseconds);

                // Save all generated assets and metadata to the database for tracking and future access.
                using (var transaction = await _dbContext.Database.BeginTransactionAsync())
                {
                    try
                    {
                        var promptEntity = new Prompt
                        {
                            UserId = userId,
                            Content = $"{request.Subject} - {request.Chapter} - Grade {request.Grade} - Template {request.Template} - video",
                            CreatedAt = DateTime.UtcNow,
                            Status = "Completed"
                        };
                        _dbContext.Prompts.Add(promptEntity);
                        await _dbContext.SaveChangesAsync();

                        var slideEntities = slides.Select((slide, i) => new Slide
                        {
                            PromptId = promptEntity.Promptid,
                            UserId = userId,
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
                        await _quotaService.CheckQuotaAsync(userId, "video");
                        await _quotaService.IncrementQuotaUsedAsync(userId, "video");
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        _logger.LogError(ex, "Database error while saving video lesson");
                        return StatusCode(500, ApiResponse<string>.Fail("Failed to save lesson data: " + ex.Message, 500));
                    }
                }

                totalSw.Stop();
                _logger.LogInformation("Total request time: {Elapsed} ms", totalSw.ElapsedMilliseconds);

                // Return the result to the client.
                return Ok(ApiResponse<SlideGenerationResultDto>.Success(new SlideGenerationResultDto
                {
                    Subject = request.Subject,
                    Chapter = request.Chapter,
                    Grade = request.Grade,
                    SlideUrl = slideUrl,
                    VideoUrl = videoUrl,
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating video lesson");
                return StatusCode(500, ApiResponse<string>.Fail("Failed to generate video lesson: " + ex.Message, 500));
            }
        }

        // Generate audio for each slide using TTS.
        // Why: Audio is required for video lessons to provide narration for each slide.
        private async Task AssignAudioToSlidesAsync(List<LessonSlideDto> slides, string lessonId)
        {
            var audioTasks = slides.Select((slide, i) => Task.Run(async () =>
            {
                var audioBlobName = $"presentations/{lessonId}/audio/{i}.wav";
                slide.AudioUrl = await _ttsService.GenerateAudioAsync(slide.Content ?? string.Empty, audioBlobName);
            }));
            await Task.WhenAll(audioTasks);
        }
    }
}