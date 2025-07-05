using Azure;
using EduVision.DBContext;
using EduVision.Models;
using EduVision.Models.DTO.Request;
using EduVision.Models.DTO.Response;
using EduVision.Services.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Net.payOS;
using Net.payOS.Types;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace EduVision.Controllers
{
    // This controller manages payment status checking and quota updates after payment.
    // API naming convention: Use plural, hyphenated nouns for resource URIs (e.g., /api/payments).
    // Parameters are passed as query or body parameters, not as route segments, to keep URIs clean and RESTful.
    [ApiController]
    [Route("api/payments")]
    public class PaymentController : ControllerBase
    {
        private readonly PayOS _payOS;
        private readonly IQuotaService _quotaService;
        private readonly EduVisionContext _context;

        // Constructor injects payment gateway, quota service, and database context.
        // Why: Follows dependency injection for testability and separation of concerns.
        public PaymentController(PayOS payOS, IQuotaService quotaService, EduVisionContext context)
        {
            _payOS = payOS;
            _quotaService = quotaService;
            _context = context;
        }

        /// <summary>
        /// Checks the payment status for a given order code and updates quota if paid.
        /// Note: Quota update now happens in OrderController.PaymentCallback for immediate processing.
        /// </summary>
        [HttpGet("status")]
        public async Task<IActionResult> CheckPaymentStatus([FromQuery] long orderCode)
        {
            // Query the payment gateway for the payment status.
            var result = await _payOS.getPaymentLinkInformation(orderCode);

            if (result.status == "PAID")
            {
                // Find the payment record in the database.
                var payment = await _context.Payments.FirstOrDefaultAsync(p => p.OrderCode == orderCode.ToString());
                if (payment != null && payment.Status == "success")
                {
                    // Payment already processed successfully
                    return Ok(ApiResponse<object>.Success(null, "Paid & Quota Updated", 0));
                }
                else if (payment != null && payment.Status != "success")
                {
                    // Payment exists but not marked as success - this shouldn't happen with new flow
                    // But we handle it as backup
                    payment.Status = "success";
                    if (payment.UserId.HasValue && payment.Amount.HasValue)
                    {
                        await _quotaService.IncreaseQuotaAsync(payment.UserId.Value, payment.Amount.Value);

                        // Create a notification for successful payment
                        var notification = new Notification
                        {
                            UserId = payment.UserId.Value,
                            Message = $"Your payment of {payment.Amount.Value} VND has been successfully processed (backup processing).",
                            CreatedAt = DateTime.UtcNow
                        };
                        _context.Notifications.Add(notification);
                        await _context.SaveChangesAsync();
                    }
                }
                // Return success response to the client.
                return Ok(ApiResponse<object>.Success(null, "Paid & Quota Updated", 0));
            }

            // Return failure response if not paid.
            return Ok(ApiResponse<object>.Fail("Not Paid", -1));
        }

        /// <summary>
        /// Retrieves the payment history (quota top-ups) for a specific user.
        /// </summary>
        [HttpGet("history")]
        public async Task<IActionResult> GetPaymentHistory([FromQuery] int userId)
        {
            var payments = await _context.Payments
                .Where(p => p.UserId == userId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            if (payments == null || payments.Count == 0)
                return NotFound(ApiResponse<object>.Fail("No payment history found for this user", 404));

            return Ok(ApiResponse<object>.Success(new { payments }));
        }

        
    }
}
