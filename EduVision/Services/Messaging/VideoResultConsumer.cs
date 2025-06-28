using Confluent.Kafka;
using EduVision.DBContext;
using EduVision.Models;
using EduVision.Models.Config;
using Microsoft.CognitiveServices.Speech.Transcription;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace EduVision.Services.Messaging
{
    public class VideoResultConsumer : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly KafkaConfig _kafkaConfig;
        private readonly ILogger<VideoResultConsumer> _logger;
        private bool _connectionAttempted = false;

        public VideoResultConsumer(
            IServiceProvider serviceProvider,
            IOptions<KafkaConfig> kafkaConfig,
            ILogger<VideoResultConsumer> logger)
        {
            _serviceProvider = serviceProvider;
            _kafkaConfig = kafkaConfig.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("VideoResultConsumer service started");
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
                        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                    }
                    _connectionAttempted = true;

                    var config = new ConsumerConfig
                    {
                        BootstrapServers = _kafkaConfig.BootstrapServers,
                        GroupId = "video-result-group",
                        AutoOffsetReset = AutoOffsetReset.Latest,
                        EnableAutoCommit = true,
                        AutoCommitIntervalMs = 5000,
                        SessionTimeoutMs = 10000,
                        SocketTimeoutMs = 10000,
                    };

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
                        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                        var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, stoppingToken);

                        consumer.Subscribe(_kafkaConfig.VideoResultTopic);
                        _logger.LogInformation("Successfully connected to Kafka. Listening to topic: {Topic}",
                            _kafkaConfig.VideoResultTopic);

                        while (!stoppingToken.IsCancellationRequested)
                        {
                            try
                            {
                                var result = consumer.Consume(TimeSpan.FromSeconds(1));
                                if (result == null) continue;

                                _ = Task.Run(async () =>
                                {
                                    try
                                    {
                                        var message = JsonSerializer.Deserialize<VideoResultKafkaMessage>(result.Message.Value);
                                        if (message == null)
                                        {
                                            _logger.LogWarning("Received null or invalid message from Kafka.");
                                            return;
                                        }

                                        await ProcessVideoResultAsync(message);
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
                                break;
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

            _logger.LogInformation("VideoResultConsumer service stopped");
        }

        private async Task ProcessVideoResultAsync(VideoResultKafkaMessage message)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<EduVisionContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<VideoResultConsumer>>();
            var userId = message.UserId;
            try
            {
                // Find related prompt
                var prompt = await dbContext.Prompts.FindAsync(message.PromptId);
                if (prompt == null)
                {
                    logger.LogError("No prompt found with ID {PromptId}", message.PromptId);
                    return;
                }

                // Find related video
                var video = await dbContext.GeneratedVideos
                    .Where(v => v.PromptId == message.PromptId)
                    .FirstOrDefaultAsync();

                if (video == null)
                {
                    logger.LogError("No video found for prompt ID {PromptId}", message.PromptId);
                    return;
                }

                if (message.Success)
                {
                    // Update status to completed
                    video.VideoUrl = message.VideoUrl;
                    video.Status = "Completed";
                    video.DurationSec = message.DurationSec;
                    video.Resolution = message.Resolution;
                    
                    // Update prompt status
                    prompt.Status = "Completed";
                    
                    // Update slide status if needed
                    var slides = await dbContext.Slides
                        .Where(s => s.PromptId == message.PromptId)
                        .ToListAsync();
                        
                    foreach (var slide in slides)
                    {
                        slide.Status = "Completed";
                    }

                    // Send FCM notification
                    var user = await dbContext.Users.FirstOrDefaultAsync(u => u.UserId == userId);
                    if (!string.IsNullOrEmpty(user?.FcmToken))
                    {
                        var fcmService = scope.ServiceProvider.GetRequiredService<FirebaseCloudMessagingService>();
                        await fcmService.SendVideoGeneratedAsync(user.FcmToken, message.VideoUrl);
                    }
                    logger.LogInformation("Video generation completed for PromptId: {PromptId}, URL: {VideoUrl}", 
                        message.PromptId, message.VideoUrl);
                }
                else
                {
                    // Update status to failed
                    video.Status = "Failed";
                    prompt.Status = "Failed";
                    
                    logger.LogError("Video generation failed for PromptId: {PromptId}, Error: {Error}", 
                        message.PromptId, message.ErrorMessage);
                }

                await dbContext.SaveChangesAsync();
                logger.LogInformation("Database updated successfully for PromptId: {PromptId}", message.PromptId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing video result for PromptId: {PromptId}", message.PromptId);
            }
        }
    }
}