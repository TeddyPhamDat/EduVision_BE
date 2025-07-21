namespace EduVision.Models.Constants
{
    /// <summary>
    /// Standard HTTP status codes used throughout the application.
    /// </summary>
    public static class HttpStatusCodes
    {
        public const int Ok = 200;
        public const int Accepted = 202;
        public const int BadRequest = 400;
        public const int Unauthorized = 401;
        public const int Forbidden = 403;
        public const int NotFound = 404;
        public const int InternalServerError = 500;
        public const int BadGateway = 502;
        public const int ServiceUnavailable = 503;
        public const int TooManyRequests = 429;
    }
}