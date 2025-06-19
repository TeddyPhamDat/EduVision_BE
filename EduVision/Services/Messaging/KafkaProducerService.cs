using Confluent.Kafka;
using EduVision.Models.Config;
using Microsoft.Extensions.Options;
using System.Text.Json;

public class KafkaProducerService
{
    private readonly IProducer<Null, string> _producer;
    private readonly string _topic;

    public KafkaProducerService(IOptions<KafkaConfig> config)
    {
        var producerConfig = new ProducerConfig
        {
            BootstrapServers = config.Value.BootstrapServers,
            SecurityProtocol = SecurityProtocol.SaslSsl,
            SaslMechanism = SaslMechanism.Plain,
            SaslUsername = config.Value.SaslUsername,
            SaslPassword = config.Value.SaslPassword
        };
        _producer = new ProducerBuilder<Null, string>(producerConfig).Build();
        _topic = config.Value.SlideGenerationTopic;
    }

    public async Task ProduceAsync<T>(T message)
    {
        var json = JsonSerializer.Serialize(message);
        await _producer.ProduceAsync(_topic, new Message<Null, string> { Value = json });
    }
}

