using Confluent.Kafka;
using EduVision.DBContext;
using EduVision.Models;
using EduVision.Models.Config;
using EduVision.Models.DTO.Request;
using EduVision.Services.AI;
using EduVision.Services.Data;
using EduVision.Services.Media;
using EduVision.Services.Presentation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace EduVision.Services.Messaging
{
    public class SlideGenerationConsumer : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly KafkaConfig _kafkaConfig;
        private readonly ILogger<SlideGenerationConsumer> _logger;
        private bool _connectionAttempted = false;

        public SlideGenerationConsumer(
            IServiceProvider serviceProvider,
            IOptions<KafkaConfig> kafkaConfig,
            ILogger<SlideGenerationConsumer> logger)
        {
            _serviceProvider = serviceProvider;
            _kafkaConfig = kafkaConfig.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("SlideGenerationConsumer service started");

            // Start consumer loop in a non-blocking way
            _ = Task.Run(() => RunConsumerAsync(stoppingToken), stoppingToken);
        }

        private async Task RunConsumerAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (_connectionAttempted)
                    {
                        // If we've already tried to connect, wait before retrying
                        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                    }
                    _connectionAttempted = true;

                    var config = new ConsumerConfig
                    {
                        BootstrapServers = _kafkaConfig.BootstrapServers,
                        GroupId = "slide-generation-group-dev2",
                        AutoOffsetReset = AutoOffsetReset.Earliest,
                        EnableAutoCommit = true,
                        AutoCommitIntervalMs = 5000,
                        SessionTimeoutMs = 10000,
                        SocketTimeoutMs = 10000,
                    };

                    // Conditionally apply security settings if provided
                    if (!string.IsNullOrEmpty(_kafkaConfig.SaslUsername) && !string.IsNullOrEmpty(_kafkaConfig.SaslPassword))
                    {
                        config.SecurityProtocol = SecurityProtocol.SaslSsl;
                        config.SaslMechanism = SaslMechanism.Plain;
                        config.SaslUsername = _kafkaConfig.SaslUsername;
                        config.SaslPassword = _kafkaConfig.SaslPassword;
                    }

                    using var consumer = new ConsumerBuilder<Ignore, string>(config).Build();

                    try
                    {
                        // Set a timeout for subscription operation
                        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                        var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, stoppingToken);

                        consumer.Subscribe(_kafkaConfig.SlideGenerationTopic);
                        _logger.LogInformation("Successfully connected to Kafka. Listening to topic: {Topic}",
                            _kafkaConfig.SlideGenerationTopic);

                        // Process messages until cancellation is requested
                        while (!stoppingToken.IsCancellationRequested)
                        {
                            try
                            {
                                // Use a short timeout to avoid blocking forever
                                var result = consumer.Consume(TimeSpan.FromSeconds(1));
                                if (result == null) continue;

                                _logger.LogInformation("Raw Kafka message: {Value}", result.Message.Value);

                                _ = Task.Run(async () =>
                                {
                                    try
                                    {
                                        var message = JsonSerializer.Deserialize<SlideGenerationKafkaMessage>(result.Message.Value);
                                        if (message == null)
                                        {
                                            _logger.LogWarning("Received null or invalid message from Kafka.");
                                            return;
                                        }

                                        await ProcessMessageAsync(message);
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogError(ex, "Error processing Kafka message");
                                    }
                                }, stoppingToken);
                            }
                            catch (ConsumeException ex)
                            {
                                _logger.LogError(ex, "Kafka consume error");
                                break; // Break inner loop to recreate consumer
                            }
                            catch (OperationCanceledException)
                            {
                                break;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error in message consumption loop");
                                await Task.Delay(1000, stoppingToken);
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogWarning("Kafka subscription operation timed out");
                    }
                    finally
                    {
                        try
                        {
                            consumer.Close();
                            _logger.LogInformation("Kafka consumer closed");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error closing Kafka consumer");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in Kafka consumer. Will retry connection in 1 minute.");
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
            }

            _logger.LogInformation("SlideGenerationConsumer service stopped");
        }

        private async Task ProcessMessageAsync(SlideGenerationKafkaMessage message)
        {
            using var scope = _serviceProvider.CreateScope();
            var geminiService = scope.ServiceProvider.GetRequiredService<IGeminiService>();
            var dbContext = scope.ServiceProvider.GetRequiredService<DBContext.EduVisionContext>();
            var revealJsGenerator = scope.ServiceProvider.GetRequiredService<RevealJsGenerator>();
            var quotaService = scope.ServiceProvider.GetRequiredService<IQuotaService>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<SlideGenerationConsumer>>();
            var slideImageSelector = scope.ServiceProvider.GetRequiredService<SlideImageSelectorService>();
            var kafkaProducer = scope.ServiceProvider.GetRequiredService<KafkaProducerService>();
            var ttsService = scope.ServiceProvider.GetRequiredService<TextToSpeechService>();

            var userId = message.UserId;
            var request = message.Request;
            var generateVideo = message.GenerateVideo;

            logger.LogInformation("Processing slide generation for UserId: {UserId}, Subject: {Subject}, Chapter: {Chapter}, GenerateVideo: {GenerateVideo}",
                userId, request.Subject, request.Chapter, generateVideo);

            try
            {
                var prompt = await dbContext.Prompts.FindAsync(message.PromptId);
                if (prompt == null)
                {
                    logger.LogError("Prompt not found for PromptId: {PromptId}", message.PromptId);
                    return;
                }

                // Generate slides
                var slideResult = await geminiService.GenerateEducationSlidesAsync(request.Subject, request.Chapter, request.Grade);

                if (slideResult == null || slideResult.HttpStatusCode != 200 || slideResult.Slides == null || slideResult.Slides.Count == 0)
                {
                    logger.LogError("Failed to generate slides for UserId: {UserId}, Reason: {Reason}", userId, slideResult?.ErrorMessage);
                    
                    if (generateVideo)
                    {
                        await UpdateFailedStatus(dbContext, userId, request, "Failed to generate slides");
                    }
                    
                    prompt.Status = "Failed";
                    await dbContext.SaveChangesAsync();
                    return;
                }

                // Assign images
                var imageCategory = string.IsNullOrEmpty(request.ImageCategory) ? "education" : request.ImageCategory;
                var imageUrls = await slideImageSelector.GetBestImagesForSlidesAsync(
                    imageCategory,
                    request.Grade,
                    request.Chapter,
                    slideResult.Slides.Count
                );

                if (imageUrls == null || imageUrls.Count < slideResult.Slides.Count)
                {
                    logger.LogError("Not enough images found for category '{Category}'", imageCategory);
                    
                    if (generateVideo)
                    {
                        await UpdateFailedStatus(dbContext, userId, request, "Not enough images found");
                    }
                    
                    prompt.Status = "Failed";
                    await dbContext.SaveChangesAsync();
                    return;
                }

                for (int i = 0; i < slideResult.Slides.Count; i++)
                    slideResult.Slides[i].ImageUrl = imageUrls[i];

                // Generate HTML
                var lessonId = Guid.NewGuid().ToString("N");
                string templateName = request.Template switch
                {
                    2 => "RevealTemplateDark.html",
                    3 => "RevealTemplateModern.html",
                    1 => "RevealTemplate.html",
                    _ => "RevealTemplateDark.html"
                };
                
                var slideUrl = await revealJsGenerator.GenerateRevealHtmlAsync(slideResult.Slides, lessonId, templateName);

                // Save to DB
                int promptId = 0;
                using var transaction = await dbContext.Database.BeginTransactionAsync();
                try
                {
                    if (generateVideo)
                    {
                        // For video generation, find the existing "Processing" prompt
                        var existingPrompt = await dbContext.Prompts
                            .Where(p => p.UserId == userId && p.Status == "Processing")
                            .OrderByDescending(p => p.CreatedAt)
                            .FirstOrDefaultAsync();

                        if (existingPrompt != null)
                        {
                            promptId = existingPrompt.Promptid;
                            
                            // Update the existing slide with the URL
                            var slide = await dbContext.Slides
                                .Where(s => s.PromptId == existingPrompt.Promptid)
                                .FirstOrDefaultAsync();
                                
                            if (slide != null)
                            {
                                slide.Url = slideUrl ?? "";
                                await dbContext.SaveChangesAsync();
                            }
                        }
                        else
                        {
                            // No prompt found, create a new one
                            logger.LogWarning("No processing prompt found for video generation. Creating a new one.");
                            
                            var promptEntity = new Prompt
                            {
                                UserId = userId,
                                Content = $"{request.Subject} - {request.Chapter} - Grade {request.Grade} - Template {request.Template} - video",
                                CreatedAt = DateTime.UtcNow,
                                Status = "Processing"
                            };
                            dbContext.Prompts.Add(promptEntity);
                            await dbContext.SaveChangesAsync();
                            
                            promptId = promptEntity.Promptid;
                            
                            var slideEntity = new Slide
                            {
                                PromptId = promptEntity.Promptid,
                                UserId = userId,
                                Type = request.Subject,
                                Url = slideUrl ?? "",
                                Status = "Processing"
                            };
                            dbContext.Slides.Add(slideEntity);
                            await dbContext.SaveChangesAsync();
                        }
                    }
                    else
                    {
                        // For slide-only generation
                        var promptEntity = new Prompt
                        {
                            UserId = userId,
                            Content = $"{request.Subject} - {request.Chapter} - Grade {request.Grade} - Template {request.Template} - slides",
                            CreatedAt = DateTime.UtcNow,
                            Status = "Completed"
                        };
                        dbContext.Prompts.Add(promptEntity);
                        await dbContext.SaveChangesAsync();
                        
                        promptId = promptEntity.Promptid;

                        var slideEntity = new Slide
                        {
                            PromptId = promptEntity.Promptid,
                            UserId = userId,
                            Type = request.Subject,
                            Url = slideUrl ?? "",
                            Status = "Completed"
                        };
                        dbContext.Slides.Add(slideEntity);
                        await dbContext.SaveChangesAsync();
                        
                        // Increment quota for slide-only generation
                        try { await quotaService.IncrementQuotaUsedAsync(userId, "slides"); }
                        catch (Exception quotaEx) { logger.LogError(quotaEx, "Failed to update quota usage after saving slides"); }
                    }

                    await transaction.CommitAsync();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Database error while saving slides lesson");
                    try { await transaction.RollbackAsync(); } catch { }
                    return;
                }

                // If video generation is requested, prepare audio and send to video consumer
                if (generateVideo)
                {
                    try
                    {
                        // Generate audio narration for each slide
                        foreach (var slide in slideResult.Slides)
                        {
                            var audioBlobName = $"presentations/{lessonId}/audio/{slideResult.Slides.IndexOf(slide)}.wav";
                            slide.AudioUrl = await ttsService.GenerateAudioAsync(slide.Content ?? string.Empty, audioBlobName);
                        }

                        // Send to video generation service
                        await kafkaProducer.ProduceAsync(
                            new VideoGenerationKafkaMessage
                            {
                                UserId = userId,
                                PromptId = promptId,
                                LessonId = lessonId,
                                SlideUrl = slideUrl,
                                Slides = slideResult.Slides
                            },
                            _kafkaConfig.VideoGenerationTopic);

                        logger.LogInformation("Sent slides to video generation service for UserId: {UserId}, LessonId: {LessonId}",
                            userId, lessonId);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to prepare or send video generation request for UserId: {UserId}", userId);
                        
                        // Update status to failed
                        await UpdateFailedStatus(dbContext, userId, request, $"Failed to initiate video processing: {ex.Message}");
                        
                        prompt.Status = "Failed";
                        await dbContext.SaveChangesAsync();
                    }
                }

                // Send FCM notification
                var user = await dbContext.Users.FirstOrDefaultAsync(u => u.UserId == userId);
                if (!string.IsNullOrEmpty(user?.FcmToken))
                {
                    var fcmService = scope.ServiceProvider.GetRequiredService<FirebaseCloudMessagingService>();
                    await fcmService.SendSlideGeneratedAsync(user.FcmToken, slideUrl);
                }

                logger.LogInformation("Slide generation completed for UserId: {UserId}, GenerateVideo: {GenerateVideo}", 
                    userId, generateVideo);
                
                prompt.Status = "Completed";
                await dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing slide generation for UserId: {UserId}", userId);
                
                if (generateVideo)
                {
                    await UpdateFailedStatus(dbContext, userId, request, $"Error: {ex.Message}");
                }
                
                var prompt = await dbContext.Prompts.FindAsync(message.PromptId);
                if (prompt != null)
                {
                    prompt.Status = "Failed";
                    await dbContext.SaveChangesAsync();
                }
            }
        }

        private async Task UpdateFailedStatus(DBContext.EduVisionContext dbContext, int userId, EducationRequestDto request, string errorMessage)
        {
            try
            {
                // Find the latest processing prompt
                var prompt = await dbContext.Prompts
                    .Where(p => p.UserId == userId && p.Status == "Processing")
                    .OrderByDescending(p => p.CreatedAt)
                    .FirstOrDefaultAsync();

                if (prompt != null)
                {
                    prompt.Status = "Failed";
                    
                    // Find related slides and update their status
                    var slides = await dbContext.Slides
                        .Where(s => s.PromptId == prompt.Promptid)
                        .ToListAsync();
                        
                    foreach (var slide in slides)
                    {
                        slide.Status = "Failed";
                    }
                    
                    // Find related video and update its status
                    var video = await dbContext.GeneratedVideos
                        .Where(v => v.PromptId == prompt.Promptid)
                        .FirstOrDefaultAsync();
                        
                    if (video != null)
                    {
                        video.Status = "Failed";
                    }
                    
                    await dbContext.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update status to Failed for UserId: {UserId}", userId);
            }
        }
    }
}