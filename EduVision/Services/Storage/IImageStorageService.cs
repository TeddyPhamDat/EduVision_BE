namespace EduVision.Services.Storage
{
    public interface IImageStorageService
    {
        // Uploads an image to a specific "folder" (blobName includes folder path)
        Task<string> UploadImageAsync(Stream fileStream, string blobName, string contentType);

        // Retrieves image URLs by category/folder, with an optional limit
        Task<List<string>> GetImagesByCategoryAsync(string category, int limit = 5);

        // You can add other storage methods (delete, list, etc.) as needed
    }
}