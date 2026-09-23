namespace ContosoDashboard.Services;

public class LocalFileStorageService : IFileStorageService
{
    private readonly string _baseStoragePath;
    private readonly ILogger<LocalFileStorageService> _logger;

    public LocalFileStorageService(IWebHostEnvironment environment, IConfiguration configuration, ILogger<LocalFileStorageService> logger)
    {
        _logger = logger;
        var relativePath = configuration["DocumentStorage:LocalUploadsPath"] ?? "AppData/uploads";
        _baseStoragePath = Path.Combine(environment.ContentRootPath, relativePath);

        if (!Directory.Exists(_baseStoragePath))
        {
            Directory.CreateDirectory(_baseStoragePath);
        }
    }

    private string GetFullPath(string relativeStorageKey)
    {
        // Normalize slashes
        var normalizedKey = relativeStorageKey.Replace('\\', '/').TrimStart('/');

        // Security check against directory traversal
        if (normalizedKey.Contains(".."))
        {
            throw new ArgumentException("Invalid storage key containing directory traversal sequence.", nameof(relativeStorageKey));
        }

        var fullPath = Path.GetFullPath(Path.Combine(_baseStoragePath, normalizedKey));

        // Ensure full path is strictly inside base storage directory
        if (!fullPath.StartsWith(_baseStoragePath, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Attempted to access path outside designated storage directory.");
        }

        return fullPath;
    }

    public async Task<string> SaveFileAsync(Stream fileStream, string relativeStorageKey)
    {
        var fullPath = GetFullPath(relativeStorageKey);
        var dir = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        try
        {
            using var destinationStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None);
            if (fileStream.CanSeek)
            {
                fileStream.Position = 0;
            }
            await fileStream.CopyToAsync(destinationStream);
            await destinationStream.FlushAsync();
            return relativeStorageKey;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save file at path: {FullPath}", fullPath);
            // Cleanup in case of partial write
            if (File.Exists(fullPath))
            {
                try { File.Delete(fullPath); } catch { /* ignore cleanup error */ }
            }
            throw;
        }
    }

    public Task<Stream?> GetFileStreamAsync(string relativeStorageKey)
    {
        try
        {
            var fullPath = GetFullPath(relativeStorageKey);
            if (!File.Exists(fullPath))
            {
                return Task.FromResult<Stream?>(null);
            }

            var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return Task.FromResult<Stream?>(stream);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read file stream for key: {RelativeStorageKey}", relativeStorageKey);
            return Task.FromResult<Stream?>(null);
        }
    }

    public Task<bool> DeleteFileAsync(string relativeStorageKey)
    {
        try
        {
            var fullPath = GetFullPath(relativeStorageKey);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete file for key: {RelativeStorageKey}", relativeStorageKey);
            return Task.FromResult(false);
        }
    }

    public Task<bool> FileExistsAsync(string relativeStorageKey)
    {
        try
        {
            var fullPath = GetFullPath(relativeStorageKey);
            return Task.FromResult(File.Exists(fullPath));
        }
        catch
        {
            return Task.FromResult(false);
        }
    }
}
