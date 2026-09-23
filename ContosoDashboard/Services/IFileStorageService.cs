namespace ContosoDashboard.Services;

public interface IFileStorageService
{
    Task<string> SaveFileAsync(Stream fileStream, string relativeStorageKey);
    Task<Stream?> GetFileStreamAsync(string relativeStorageKey);
    Task<bool> DeleteFileAsync(string relativeStorageKey);
    Task<bool> FileExistsAsync(string relativeStorageKey);
}
