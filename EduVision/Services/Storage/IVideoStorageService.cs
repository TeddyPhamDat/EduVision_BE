namespace EduVision.Services.Storage
{
    public interface IVideoStorageService
    {
        Task<string> UploadAsync(string localFilePath, string blobName, string contentType);
    }
}