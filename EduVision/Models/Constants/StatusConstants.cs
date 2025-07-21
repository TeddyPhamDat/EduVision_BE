namespace EduVision.Models.Constants
{
    /// <summary>
    /// Status strings used for tracking processing states.
    /// </summary>
    public static class StatusConstants
    {
        /// <summary>
        /// Processing status for slides, videos, and prompts.
        /// </summary>
        public static class ProcessingStatus
        {
            public const string Processing = "Processing";
            public const string Completed = "Completed";
            public const string Failed = "Failed";
        }

        /// <summary>
        /// Payment status values.
        /// </summary>
        public static class PaymentStatus
        {
            public const string Success = "success";
            public const string Pending = "pending";
            public const string Cancelled = "cancelled";
            public const string Failed = "failed";
        }

        /// <summary>
        /// PayOS payment gateway status values.
        /// </summary>
        public static class PayOSStatus
        {
            public const string Paid = "PAID";
            public const string Cancelled = "CANCELLED";
            public const string Failed = "FAILED";
            public const string Pending = "PENDING";
            public const string Processing = "PROCESSING";
        }
    }
}