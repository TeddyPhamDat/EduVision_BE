using Azure;
using EduVision.DBContext;
using EduVision.Models;
using EduVision.Models.DTO.Request;
using EduVision.Models.DTO.Response;
using EduVision.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Net.payOS;
using Net.payOS.Types;
using System;
using System.Text.Json;

namespace EduVision.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        private readonly PayOS _payOS;
        private readonly IQuotaService _quotaService; // Giả sử bạn có service để xử lý quota
        private readonly EduVisionContext _context;

        public PaymentController(PayOS payOS, IQuotaService quotaService, EduVisionContext context)
        {
            _payOS = payOS;
            _quotaService = quotaService;
            _context = context;
        }

        [HttpGet("check-status")]
        public async Task<IActionResult> CheckPaymentStatus([FromQuery] long orderCode)
        {
            var result = await _payOS.getPaymentLinkInformation(orderCode);

            if (result.status == "PAID")
            {
                var payment = await _context.Payments.FirstOrDefaultAsync(p => p.OrderCode == orderCode.ToString());
                if (payment != null && payment.Status != "success")
                {
                    payment.Status = "success";
                    if (payment.UserId.HasValue && payment.Amount.HasValue)
                    {
                        await _quotaService.IncreaseQuotaAsync(payment.UserId.Value, payment.Amount.Value);
                    }
                    await _context.SaveChangesAsync();
                }
                return Ok(ApiResponse<object>.Success(null, "Paid & Quota Updated", 0));
            }

            return Ok(ApiResponse<object>.Fail("Not Paid", -1));
        }
    }
}
