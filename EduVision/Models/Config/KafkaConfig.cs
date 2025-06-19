namespace EduVision.Models.Config
{
    public class KafkaConfig
    {
        public string BootstrapServers { get; set; }
        public string SlideGenerationTopic { get; set; }
        public string SaslUsername { get; set; }
        public string SaslPassword { get; set; }
        public string SecurityProtocol { get; set; }
        public string SaslMechanism { get; set; }
    }
}
