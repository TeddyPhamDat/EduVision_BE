namespace EduVision.Services
{
    public interface IVideoStorageService
    {
        Task<string> UploadAsync(string localFilePath, string blobName, string contentType);
    }
}