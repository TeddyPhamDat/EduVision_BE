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
using Microsoft.AspNetCore.Authorization; // Added for Authorize attribute
using System.Security.Claims; // Added for ClaimTypes
using System.Collections.Generic; // Added for List

namespace EduVision.Controllers
{
    // This controller manages payment status checking and quota updates after payment.
    // API naming convention: Use plural, hyphenated nouns for resource URIs (e.g., /api/payments).
    // Parameters are passed as query or body parameters, not as route segments, to keep URIs clean and RESTful.
    [Authorize] // Added Authorize attribute for the whole controller
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
            // This API should ideally be called internally or by a webhook, or secured differently
            // For simplicity, keeping it as is for now, assuming its usage context.
            // No userId check here as it's for external callback status verification.

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
        /// Retrieves the payment history (quota top-ups) for the authenticated user.
        /// </summary>
        [HttpGet("history")]
        public async Task<IActionResult> GetPaymentHistory()
        {
            var userId = await GetAuthenticatedUserIdAsync();
            if (userId == null)
                return Unauthorized(ApiResponse<string>.Fail("User ID not found in token", 401));

            var payments = await _context.Payments
                .Where(p => p.UserId == userId.Value)
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new PaymentDetailResponseDto
                {
                    PaymentId = p.PaymentId,
                    UserId = p.UserId,
                    Amount = p.Amount,
                    Status = p.Status,
                    CreatedAt = p.CreatedAt,
                    OrderCode = p.OrderCode
                })
                .ToListAsync();

            if (payments == null || payments.Count == 0)
                return NotFound(ApiResponse<string>.Fail("No payment history found for this user", 404));

            return Ok(ApiResponse<List<PaymentDetailResponseDto>>.Success(payments));
        }

        /// <summary>
        /// Retrieves details of a specific payment transaction for the authenticated user.
        /// </summary>
        [HttpGet("{paymentId:int}")]
        public async Task<IActionResult> GetPaymentDetail(int paymentId)
        {
            var userId = await GetAuthenticatedUserIdAsync();
            if (userId == null)
                return Unauthorized(ApiResponse<string>.Fail("User ID not found in token", 401));

            var payment = await _context.Payments
                .Where(p => p.PaymentId == paymentId && p.UserId == userId.Value)
                .FirstOrDefaultAsync();

            if (payment == null)
                return NotFound(ApiResponse<PaymentDetailResponseDto>.Fail("Payment not found or does not belong to user", 404));

            var responseDto = new PaymentDetailResponseDto
            {
                PaymentId = payment.PaymentId,
                UserId = payment.UserId,
                Amount = payment.Amount,
                Status = payment.Status,
                CreatedAt = payment.CreatedAt,
                OrderCode = payment.OrderCode
            };

            return Ok(ApiResponse<PaymentDetailResponseDto>.Success(responseDto));
        }

        // Helper method to get authenticated user ID
        private async Task<int?> GetAuthenticatedUserIdAsync()
        {
            var userIdClaim = User.FindFirst("userId") ?? User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
                return null;
            return userId;
        }
    }
}
