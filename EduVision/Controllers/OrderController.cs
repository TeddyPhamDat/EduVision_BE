using EduVision.DBContext;
using EduVision.Models;
using EduVision.Models.DTO.Request;
using EduVision.Models.DTO.Response;
using EduVision.Services.Data;
using Microsoft.AspNetCore.Mvc;
using Net.payOS;
using Net.payOS.Types;
using Microsoft.EntityFrameworkCore;

namespace EduVision.Controllers
{
    // This controller manages payment order creation for quota top-up.
    [ApiController]
    [Route("api/orders")]
    public class OrderController : ControllerBase
    {
        private readonly PayOS _payOS;
        private readonly EduVisionContext _context;
        private readonly IQuotaService _quotaService;

        // Constructor injects payment gateway, database context, and quota service.
        // Why: Follows dependency injection for testability and separation of concerns.
        public OrderController(PayOS payOS, EduVisionContext context, IQuotaService quotaService)
        {
            _payOS = payOS;
            _context = context;
            _quotaService = quotaService;
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

                // Use backend callback URLs instead of direct frontend URLs
                string baseUrl = $"{Request.Scheme}://{Request.Host}";
                string returnUrl = $"{baseUrl}/api/orders/payment-callback?orderCode={orderCode}&status=success&frontendUrl={Uri.EscapeDataString(request.ReturnUrl)}";
                string cancelUrl = $"{baseUrl}/api/orders/payment-callback?orderCode={orderCode}&status=cancel&frontendUrl={Uri.EscapeDataString(request.CancelUrl)}";

                // Prepare payment data for the external payment gateway.
                var paymentData = new PaymentData(
                    orderCode,
                    request.Amount,
                    $"Nạp quota cho user {request.UserId}",
                    items,
                    returnUrl,
                    cancelUrl,
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

        /// <summary>
        /// Handles payment callback from PayOS and redirects to frontend
        /// </summary>
        [HttpGet("payment-callback")]
        public async Task<IActionResult> PaymentCallback(
            [FromQuery] long orderCode, 
            [FromQuery] string status, 
            [FromQuery] string frontendUrl)
        {
            try
            {
                // Get payment from database first
                var payment = await _context.Payments.FirstOrDefaultAsync(p => p.OrderCode == orderCode.ToString());
                if (payment == null)
                {
                    return Redirect($"{frontendUrl}?orderCode={orderCode}&status=error&message=Payment+not+found");
                }

                // Check payment status with PayOS to get the actual status
                var payOSResult = await _payOS.getPaymentLinkInformation(orderCode);
                string finalStatus = "failed";
                string redirectStatus = "failed";

                // Simplified status logic: only success or cancelled
                switch (payOSResult.status.ToUpper())
                {
                    case "PAID":
                        finalStatus = "success";
                        redirectStatus = "success";
                        
                        // Update payment status if not already done
                        if (payment.Status != "success")
                        {
                            payment.Status = "success";
                            Console.WriteLine($"Payment {orderCode} marked as success in database");
                            
                            // Increase quota immediately upon successful payment
                            if (payment.UserId.HasValue && payment.Amount.HasValue)
                            {
                                await _quotaService.IncreaseQuotaAsync(payment.UserId.Value, payment.Amount.Value);
                                Console.WriteLine($"Quota increased for user {payment.UserId.Value} by {payment.Amount.Value}");
                                
                                // Create a notification for successful payment
                                var notification = new Notification
                                {
                                    UserId = payment.UserId.Value,
                                    Message = $"Your payment of {payment.Amount.Value} VND has been successfully processed and quota updated.",
                                    CreatedAt = DateTime.UtcNow
                                };
                                _context.Notifications.Add(notification);
                                Console.WriteLine($"Notification created for user {payment.UserId.Value}");
                            }
                        }
                        break;

                    default:
                        // All other statuses (CANCELLED, FAILED, PENDING, PROCESSING) → cancelled
                        finalStatus = "cancelled";
                        redirectStatus = "cancelled";
                        payment.Status = "cancelled";
                        Console.WriteLine($"Payment {orderCode} marked as cancelled with PayOS status: {payOSResult.status}");
                        break;
                }

                // Update payment status in database
                await _context.SaveChangesAsync();
                Console.WriteLine($"Payment {orderCode} database updated with status: {payment.Status}");

                // Create appropriate redirect URL based on status
                string redirectUrl = $"{frontendUrl}?orderCode={orderCode}&status={redirectStatus}";
                
                // Add reason for cancelled payments
                if (redirectStatus == "cancelled")
                {
                    if (payOSResult.status.ToUpper() == "CANCELLED")
                    {
                        redirectUrl += "&reason=user_cancelled";
                    }
                    else if (payOSResult.status.ToUpper() == "FAILED")
                    {
                        redirectUrl += "&reason=payment_failed";
                    }
                    else
                    {
                        redirectUrl += "&reason=timeout_or_other";
                    }
                }

                Console.WriteLine($"Redirecting to: {redirectUrl}");
                return Redirect(redirectUrl);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Payment callback error: {ex.Message}");
                // Update payment as failed in case of error
                try
                {
                    var payment = await _context.Payments.FirstOrDefaultAsync(p => p.OrderCode == orderCode.ToString());
                    if (payment != null && payment.Status == "pending")
                    {
                        payment.Status = "failed";
                        await _context.SaveChangesAsync();
                        Console.WriteLine($"Payment {orderCode} marked as failed due to system error");
                    }
                }
                catch (Exception dbEx)
                {
                    Console.WriteLine($"Database update error: {dbEx.Message}");
                }
                
                // Redirect to frontend error page
                return Redirect($"{frontendUrl}?orderCode={orderCode}&status=error&message=System+error");
            }
        }
    }
}
