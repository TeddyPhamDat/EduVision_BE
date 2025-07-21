namespace EduVision.Models.Constants
{
    /// <summary>
    /// Standardized success messages used throughout the application.
    /// </summary>
    public static class SuccessMessages
    {
        /// <summary>
        /// Video generation messages.
        /// </summary>
        public static class Video
        {
            public const string GenerationAccepted = "Video generation request accepted and is being processed. Use the returned ID to check the status.";
            public const string GenerationSubmitted = "Your video generation request has been submitted and is being processed.";
        }

        /// <summary>
        /// Slide generation messages.
        /// </summary>
        public static class Slides
        {
            public const string GenerationAccepted = "Slide generation request accepted and is being processed.";
            public const string GenerationSubmitted = "Your slide generation request has been submitted and is being processed.";
        }

        /// <summary>
        /// Notification messages.
        /// </summary>
        public static class Notifications
        {
            public const string MarkedAsRead = "Notification marked as read.";
            public const string AllMarkedAsRead = "All notifications marked as read.";
            public const string NoUnreadNotifications = "No unread notifications.";
            public const string Deleted = "Notification deleted.";
        }

        /// <summary>
        /// Payment messages.
        /// </summary>
        public static class Payment
        {
            public const string PaidAndQuotaUpdated = "Paid & Quota Updated";
            public const string ProcessingSuccessful = "Your payment has been successfully processed and quota updated.";
        }
    }
}