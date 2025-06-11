using EduVision.DBContext;
using EduVision.Models;
using EduVision.Models.DTO;
using EduVision.Models.DTO.Request;
using EduVision.Models.DTO.Response;
using EduVision.Models.Entities.Enum;
using EduVision.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Security.Claims;

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
        private readonly IQuotaService _quotaService;

        public EducationController(
            GeminiService geminiService,
            CloudinaryImageService imageService,
            RevealJsGenerator revealJsGenerator,
            TextToSpeechService ttsService,
            SlideCaptureService slideCaptureService,
            VideoGenerationService videoGenerationService,
            MongoDbService mongoDbService,
            EduVisionContext dbContext,
            ILogger<EducationController> logger,
            IQuotaService quotaService)
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
            _quotaService = quotaService;
        }

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


        [Authorize(Roles = "USER")]
        [HttpPost("generate")]
        [ProducesResponseType(typeof(SlideGenerationResultDto), 200)]
        public async Task<ActionResult<SlideGenerationResultDto>> GenerateEducationLesson([FromBody] EducationRequestDto request)
        {
            var userIdClaim = User.FindFirst("userId") ?? User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized(ApiResponse<string>.Fail("User ID not found in token", 401));

            }

            // --- B??c ki?m tra quota ---
            var quotaType = request.Mode?.ToLower() switch
            {
                "slides" => "slides",
                "video" => "video",
                _ => throw new ArgumentException("Invalid mode")
            };
            // L?y user
            var user = await _dbContext.Users.FindAsync(userId);

            if (user == null)
            {
                return BadRequest(ApiResponse<string>.Fail("User does not exist", 404));
            }

            // N?u là manager thì b? qua quota check
           
                // Correcting the condition to properly compare the user's role
                if ((Role)user.Role != Role.MANAGER) {
                bool hasQuota = await _quotaService.CheckQuotaAsync(userId, quotaType);
                if (!hasQuota)
                {
                    return BadRequest(ApiResponse<string>.Fail("You have exceeded the number of lesson creations for the month", 400));
                }
            }
               
            
        
            // --- End ki?m tra quota ---

            if (string.IsNullOrEmpty(request.Subject) || string.IsNullOrEmpty(request.Chapter))
            {
                return BadRequest(ApiResponse<string>.Fail("Subject and chapter parameters are required", 400));

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
                     ApiResponse<string>.Fail(slideResult?.ErrorMessage ?? "Failed to generate slides", slideResult?.HttpStatusCode ?? 500)
                    );

                   
                }

                var slides = slideResult.Slides;
                if (slides == null || slides.Count == 0)
                {
                    return BadRequest(ApiResponse<string>.Fail("No slides generated", 400));

                }

                var imageUrls = await GetBestImagesForSlidesAsync(imageCategory, request.Grade, request.Chapter, slides.Count);

                if (imageUrls == null || imageUrls.All(string.IsNullOrEmpty))
                {
                    return BadRequest(ApiResponse<string>.Fail($"No images found for category '{imageCategory}'", 400));

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
                    return StatusCode(500, ApiResponse<string>.Fail("Failed to generate presentation: " + ex.Message, 500));

                }

                // Save to DB for "slides" mode
                if (request.Mode == "slides")
                {
                    bool saved = false;

                    using var transaction = await _dbContext.Database.BeginTransactionAsync();
                    try
                    {
                        var promptEntity = new Prompt
                        {
                            UserId = userId,
                            Content = $"{request.Subject} - {request.Chapter} - Grade {request.Grade} - Template {request.Template} - {request.Mode}",
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
                            Url = slide.ImageUrl ?? "",
                            Status = "Completed"
                        }).ToList();

                        _dbContext.Slides.AddRange(slideEntities);
                        await _dbContext.SaveChangesAsync();

                        await transaction.CommitAsync();
                        saved = true;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Database error while saving slides lesson");
                        try
                        {
                            await transaction.RollbackAsync();
                        }
                        catch (Exception rollbackEx)
                        {
                            _logger.LogError(rollbackEx, "Failed to rollback transaction");
                        }
                        return StatusCode(500, ApiResponse<string>.Fail("Failed to save lesson data: " + ex.Message, 500));
                    }

                    if (saved)
                    {
                        try
                        {
                            await _quotaService.IncrementQuotaUsedAsync(userId, "slides");
                        }
                        catch (Exception quotaEx)
                        {
                            _logger.LogError(quotaEx, "Failed to update quota usage after saving slides");
                            // Optional: rollback quota if needed
                        }
                    }

                    totalSw.Stop();
                    _logger.LogInformation("Total request time: {Elapsed} ms", totalSw.ElapsedMilliseconds);

                    return Ok(ApiResponse<object>.Success(new
                    {
                        Subject = request.Subject,
                        Chapter = request.Chapter,
                        Grade = request.Grade,
                        SlideUrl = slideUrl
                    }));
                }

            

                // Continue for "video" mode
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

                // Save to DB for "video" mode
                using (var transaction = await _dbContext.Database.BeginTransactionAsync())
                {
                    try
                    {
                        var promptEntity = new Prompt
                        {
                            UserId = userId,
                            Content = $"{request.Subject} - {request.Chapter} - Grade {request.Grade} - Template {request.Template} - {request.Mode}",
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

                return Ok(ApiResponse<object>.Success(new
                {
                    Subject = request.Subject,
                    Chapter = request.Chapter,
                    Grade = request.Grade,
                    SlideUrl = slideUrl,
                    VideoUrl = videoUrl
                }));


            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating education lesson");
                return StatusCode(500, ApiResponse<string>.Fail("Failed to generate education lesson: " + ex.Message, 500));

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