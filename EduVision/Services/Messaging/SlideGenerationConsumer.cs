using Confluent.Kafka;
using EduVision.Models;
using EduVision.Models.Config;
using EduVision.Models.DTO.Request;
using EduVision.Services.AI;
using EduVision.Services.Data;
using EduVision.Services.Presentation;
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
                        GroupId = "slide-generation-group",
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

            var userId = message.UserId;
            var request = message.Request;

            logger.LogInformation("Processing slide generation for UserId: {UserId}, Subject: {Subject}, Chapter: {Chapter}",
                userId, request.Subject, request.Chapter);

            try
            {
                // Generate slides
                var slideResult = await geminiService.GenerateEducationSlidesAsync(request.Subject, request.Chapter, request.Grade);

                if (slideResult == null || slideResult.HttpStatusCode != 200 || slideResult.Slides == null || slideResult.Slides.Count == 0)
                {
                    logger.LogError("Failed to generate slides for UserId: {UserId}, Reason: {Reason}", userId, slideResult?.ErrorMessage);
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
                using var transaction = await dbContext.Database.BeginTransactionAsync();
                try
                {
                    var promptEntity = new Prompt
                    {
                        UserId = userId,
                        Content = $"{request.Subject} - {request.Chapter} - Grade {request.Grade} - Template {request.Template} - slides",
                        CreatedAt = DateTime.UtcNow,
                        Status = "Completed"
                    };
                    dbContext.Prompts.Add(promptEntity);
                    await dbContext.SaveChangesAsync();

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

                    await transaction.CommitAsync();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Database error while saving slides lesson");
                    try { await transaction.RollbackAsync(); } catch { }
                    return;
                }

                // Increment quota
                try { await quotaService.IncrementQuotaUsedAsync(userId, "slides"); }
                catch (Exception quotaEx) { logger.LogError(quotaEx, "Failed to update quota usage after saving slides"); }

                logger.LogInformation("Slide generation completed for UserId: {UserId}", userId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing slide generation for UserId: {UserId}", userId);
            }
        }
    }
}