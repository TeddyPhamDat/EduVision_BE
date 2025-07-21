namespace EduVision.Models.Constants
{
    /// <summary>
    /// Configuration constants for various services.
    /// </summary>
    public static class ServiceConstants
    {
        /// <summary>
        /// Gemini AI service configuration.
        /// </summary>
        public static class Gemini
        {
            public const int MaxRetries = 10;
            public const int InitialDelayMs = 1000;
            public const int MaxContentLength = 8000;
            public const int DefaultSlideCount = 5;
            public const string CodeBlockMarker = "```";
        }

        /// <summary>
        /// Presentation template configuration.
        /// </summary>
        public static class Presentation
        {
            public const int DefaultWidth = 1920;
            public const int DefaultHeight = 1080;
            public const string DefaultImageCategory = "education";
        }

        /// <summary>
        /// Pagination defaults and limits.
        /// </summary>
        public static class Pagination
        {
            public const int DefaultPageSize = 10;
            public const int MaxPageSize = 100;
            public const int MinPage = 1;
        }

        /// <summary>
        /// Kafka consumer configuration.
        /// </summary>
        public static class Kafka
        {
            public const int SessionTimeoutMs = 10000;
            public const int SocketTimeoutMs = 10000;
            public const int ConsumeTimeoutSeconds = 1;
            public const int ConnectionTimeoutSeconds = 10;
            public const int RetryDelayMinutes = 1;
            public const int MessageDelayMs = 1000;
            public const string SlideGroupId = "slide-generation-group-dev2";
            public const string VideoResultGroupId = "video-result-group";
        }

        /// <summary>
        /// Cache duration constants (in seconds).
        /// </summary>
        public static class Cache
        {
            public const int SubjectsCacheDuration = 3600; // 1 hour
            public const int ChaptersCacheDuration = 1800; // 30 minutes
        }

        /// <summary>
        /// Audio and file format constants.
        /// </summary>
        public static class Audio
        {
            public const string WavExtension = ".wav";
            public const string AudioPathTemplate = "presentations/{0}/audio/{1}.wav";
        }
    }
}