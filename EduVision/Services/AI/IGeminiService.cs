using EduVision.Models.DTO;
using System.Threading.Tasks;

namespace EduVision.Services.AI
{
    public interface IGeminiService
    {
        Task<SlideGenerationResultDto> GenerateEducationSlidesAsync(string subject, string chapter, int? grade = null);
    }
}