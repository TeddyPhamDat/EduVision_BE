namespace EduVision.Models.Config
{
    public class KafkaConfig
    {
        public required string BootstrapServers { get; set; }
        public required string SlideGenerationTopic { get; set; }
        public required string VideoGenerationTopic { get; set; }
        public required string VideoResultTopic { get; set; }
        public required string SaslUsername { get; set; }
        public required string SaslPassword { get; set; }
        public required string SecurityProtocol { get; set; }
        public required string SaslMechanism { get; set; }
    }
}
