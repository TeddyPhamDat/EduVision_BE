using EduVision.DBContext;
using EduVision.Models;
using EduVision.Models.DTO.Request;
using EduVision.Models.DTO.Response;
using Microsoft.AspNetCore.Mvc;
using Net.payOS;
using Net.payOS.Types;

namespace EduVision.Controllers
{
    // This controller manages payment order creation for quota top-up.
    [ApiController]
    [Route("api/orders")]
    public class OrderController : ControllerBase
    {
        private readonly PayOS _payOS;
        private readonly EduVisionContext _context;

        // Constructor injects payment gateway and database context.
        // Why: Follows dependency injection for testability and separation of concerns.
        public OrderController(PayOS payOS, EduVisionContext context)
        {
            _payOS = payOS;
            _context = context;
        }

        /// <summary>
        /// Creates a payment link for quota top-up. Allows users to initiate a payment process for increasing their quota.
        /// </summary>
        [HttpPost("payment-links")]
        public async Task<IActionResult> CreatePaymentLink([FromBody] CreatePaymentRequest request)
        {
            try
            {
                // Generate a unique order code using the current timestamp.
                // Why: Ensures each payment order is unique and traceable.
                int userId = request.UserId;
                long orderCode = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                // Prepare item details for the payment gateway.
                var items = new List<ItemData>
                {
                    new ItemData("Nạp quota", 1, request.Amount)
                };

                // Save the payment intent to the database for tracking and reconciliation.
                var payment = new Payment
                {
                    OrderCode = orderCode.ToString(),
                    UserId = userId,
                    Amount = request.Amount,
                    Status = "pending",
                    CreatedAt = DateTime.UtcNow
                };
                _context.Payments.Add(payment);
                await _context.SaveChangesAsync();

                // Prepare payment data for the external payment gateway.
                var paymentData = new PaymentData(
                    orderCode,
                    request.Amount,
                    $"Nạp quota cho user {request.UserId}",
                    items,
                    request.ReturnUrl,
                    request.CancelUrl,
                    request.UserId.ToString() // Store userId in order note for reference.
                );

                // Create the payment link using the payment gateway.
                var createPayment = await _payOS.createPaymentLink(paymentData);

                // Return the payment link to the client.
                return Ok(ApiResponse<object>.Success(createPayment, "Success", 0));
            }
            catch (Exception ex)
            {
                // Log the error and return a generic failure response.
                Console.WriteLine(ex);
                return Ok(ApiResponse<object>.Fail("Failed", -1));
            }
        }
    }
}
