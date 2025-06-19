using EduVision.Models.DTO;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EduVision.Services.Data
{
    public interface IMongoDbService
    {
        string GetCollectionForSubject(string subject);
        Task<string> GetContentAsync(string subject, string chapter, int? grade = null);
        Task<EducationalItemMetadataDto?> GetMetadataAsync(string subject, string chapter, int? grade = null);
        Task<List<string>> GetChaptersAsync(string subject, int? grade = null);
        Task<List<string>> GetAvailableSubjectsAsync();
    }
}