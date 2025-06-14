using EduVision.DBContext;
using EduVision.Models;
using EduVision.Models.DTO.Request;
using EduVision.Models.DTO.Response;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CognitiveServices.Speech.Transcription;
using Net.payOS;
using Net.payOS.Types;

namespace EduVision.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrderController : ControllerBase
    {
        private readonly PayOS _payOS;
        private readonly EduVisionContext _context;

        public OrderController(PayOS payOS, EduVisionContext context)
        {
            _payOS = payOS;
            _context = context;
        }

        [HttpPost("create")]
        public async Task<IActionResult> CreatePaymentLink([FromBody] CreatePaymentRequest request)
        {
            try
            {
                int userId = request.UserId;
                long orderCode = DateTimeOffset.UtcNow.ToUnixTimeSeconds(); // Change type to 'long'
                var items = new List<ItemData>
                {
                    new ItemData("Nạp quota", 1, request.Amount)
                };
                var payment = new Payment
                {
                    OrderCode = orderCode.ToString(), // Convert 'long' to 'string' for storage
                    UserId = userId,
                    Amount = request.Amount,
                    Status = "pending",
                    CreatedAt = DateTime.UtcNow
                };
                _context.Payments.Add(payment);
                await _context.SaveChangesAsync();

                var paymentData = new PaymentData(
                    orderCode, // Pass 'long' type as required by PaymentData
                    request.Amount,
                    $"Nạp quota cho user {request.UserId}",
                    items,
                    request.ReturnUrl,
                    request.CancelUrl,
                    request.UserId.ToString() // Ghi userId vào orderNote
                );

                var createPayment = await _payOS.createPaymentLink(paymentData);

                return Ok(ApiResponse<object>.Success(createPayment, "Success", 0));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return Ok(ApiResponse<object>.Fail("Failed", -1));
            }
        }
    }
}
