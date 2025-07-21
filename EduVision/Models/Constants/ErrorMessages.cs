namespace EduVision.Models.Constants
{
    /// <summary>
    /// Standardized error messages used throughout the application.
    /// </summary>
    public static class ErrorMessages
    {
        /// <summary>
        /// Authentication and authorization errors.
        /// </summary>
        public static class Auth
        {
            public const string UserIdNotFound = "User ID not found in token";
            public const string UserDoesNotExist = "User does not exist";
            public const string InvalidCredentials = "Invalid credentials";
        }

        /// <summary>
        /// Quota-related errors.
        /// </summary>
        public static class Quota
        {
            public const string VideoQuotaExceeded = "You have exceeded the number of video generations for the month";
            public const string SlideQuotaExceeded = "You have exceeded the number of slide generations for the month";
        }

        /// <summary>
        /// Validation errors.
        /// </summary>
        public static class Validation
        {
            public const string SubjectAndChapterRequired = "Subject and chapter parameters are required";
            public const string SubjectRequired = "Subject parameter is required";
        }

        /// <summary>
        /// Processing errors.
        /// </summary>
        public static class Processing
        {
            public const string FailedToGenerateSlides = "Failed to generate slides";
            public const string NotEnoughImages = "Not enough images found";
            public const string FailedToStartVideoGeneration = "Failed to start video generation";
        }

        /// <summary>
        /// Resource not found errors.
        /// </summary>
        public static class NotFound
        {
            public const string NotificationNotFound = "Notification not found or does not belong to user";
            public const string PaymentNotFound = "Payment not found or does not belong to user";
            public const string SlideNotFound = "Slide not found";
            public const string NoPaymentHistory = "No payment history found for this user";
        }
    }
}