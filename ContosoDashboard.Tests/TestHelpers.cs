using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ContosoDashboard.Models;
using ContosoDashboard.Services;

namespace ContosoDashboard.Tests;

public class FakeFileStorageService : IFileStorageService
{
    public Dictionary<string, byte[]> Files { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Task<string> SaveFileAsync(Stream fileStream, string relativeStorageKey)
    {
        using var ms = new MemoryStream();
        fileStream.CopyTo(ms);
        Files[relativeStorageKey] = ms.ToArray();
        return Task.FromResult(relativeStorageKey);
    }

    public Task<Stream?> GetFileStreamAsync(string relativeStorageKey)
    {
        if (Files.TryGetValue(relativeStorageKey, out var bytes))
        {
            return Task.FromResult<Stream?>(new MemoryStream(bytes));
        }
        return Task.FromResult<Stream?>(null);
    }

    public Task<bool> DeleteFileAsync(string relativeStorageKey)
    {
        return Task.FromResult(Files.Remove(relativeStorageKey));
    }

    public Task<bool> FileExistsAsync(string relativeStorageKey)
    {
        return Task.FromResult(Files.ContainsKey(relativeStorageKey));
    }
}

public class FakeNotificationService : INotificationService
{
    public List<Notification> Notifications { get; } = new();

    public Task<List<Notification>> GetUserNotificationsAsync(int userId, bool unreadOnly = false)
    {
        var query = Notifications.Where(n => n.UserId == userId);
        if (unreadOnly) query = query.Where(n => !n.IsRead);
        return Task.FromResult(query.ToList());
    }

    public Task<Notification> CreateNotificationAsync(Notification notification)
    {
        notification.NotificationId = Notifications.Count + 1;
        Notifications.Add(notification);
        return Task.FromResult(notification);
    }

    public Task<bool> MarkAsReadAsync(int notificationId, int requestingUserId)
    {
        var item = Notifications.FirstOrDefault(n => n.NotificationId == notificationId && n.UserId == requestingUserId);
        if (item != null)
        {
            item.IsRead = true;
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<int> GetUnreadCountAsync(int userId)
    {
        return Task.FromResult(Notifications.Count(n => n.UserId == userId && !n.IsRead));
    }
}

public static class TestServiceFactory
{
    public static ILogger<T> CreateLogger<T>() => NullLogger<T>.Instance;
}
