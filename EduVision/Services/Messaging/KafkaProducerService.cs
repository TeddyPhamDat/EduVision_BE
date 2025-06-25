using Confluent.Kafka;
using EduVision.Models.Config;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace EduVision.Services.Messaging
{
    public class KafkaProducerService
    {
        private readonly IProducer<Null, string> _producer;
        private readonly KafkaConfig _kafkaConfig;
        private readonly ILogger<KafkaProducerService> _logger;

        public KafkaProducerService(
            IOptions<KafkaConfig> config,
            ILogger<KafkaProducerService> logger)
        {
            _kafkaConfig = config.Value;
            _logger = logger;

            var producerConfig = new ProducerConfig
            {
                BootstrapServers = _kafkaConfig.BootstrapServers,
            };

            if (!string.IsNullOrEmpty(_kafkaConfig.SaslUsername))
            {
                producerConfig.SecurityProtocol = SecurityProtocol.SaslSsl;
                producerConfig.SaslMechanism = SaslMechanism.Plain;
                producerConfig.SaslUsername = _kafkaConfig.SaslUsername;
                producerConfig.SaslPassword = _kafkaConfig.SaslPassword;
            }
            
            _producer = new ProducerBuilder<Null, string>(producerConfig).Build();
        }

        public async Task ProduceAsync<T>(T message, string? topic = null)
        {
            var targetTopic = topic ?? _kafkaConfig.SlideGenerationTopic; // Default if not specified
            var json = JsonSerializer.Serialize(message);
            
            try 
            {
                _logger.LogInformation("Producing message to topic {Topic}", targetTopic);
                await _producer.ProduceAsync(targetTopic, new Message<Null, string> { Value = json });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error producing message to topic {Topic}", targetTopic);
                throw;
            }
        }
    }
}

