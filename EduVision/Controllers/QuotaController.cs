using EduVision.Models.DTO.Request;
using EduVision.Models.DTO.Response;
using EduVision.Services;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace EduVision.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class QuotaController : ControllerBase
    {
        private readonly IQuotaService _quotaService;

        public QuotaController(IQuotaService quotaService)
        {
            _quotaService = quotaService;
        }

        [HttpGet("check")]
        public async Task<IActionResult> CheckQuota([FromQuery] int userId, [FromQuery] string quotaType)
        {
            var canUse = await _quotaService.CheckQuotaAsync(userId, quotaType);
            var result = new { CanUse = canUse };
            var message = canUse ? "Quota is available." : "Quota limit reached.";
            return Ok(ApiResponse<object>.Success(result, message));
        }

        [HttpPost("use")]
        public async Task<IActionResult> UseQuota([FromBody] QuotaRequest request)
        {
            await _quotaService.IncrementQuotaUsedAsync(request.UserId, request.QuotaType);
            return Ok(ApiResponse<object>.Success("", "Quota used successfully."));
        }
    }
}
