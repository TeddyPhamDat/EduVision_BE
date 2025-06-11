namespace EduVision.Models.DTO.Request
{
    public class CreatePaymentRequest
    {
        public int UserId { get; set; }
        public int Amount { get; set; }
        public string ReturnUrl { get; set; }
        public string CancelUrl { get; set; }
    }
}
