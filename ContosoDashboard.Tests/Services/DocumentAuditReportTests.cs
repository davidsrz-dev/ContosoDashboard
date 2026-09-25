using ContosoDashboard.Models;
using ContosoDashboard.Services;

namespace ContosoDashboard.Tests.Services;

public class DocumentAuditReportTests
{
    private readonly FakeFileStorageService _fileStorage = new();
    private readonly FakeNotificationService _notificationService = new();

    private DocumentService CreateService(Data.ApplicationDbContext context)
    {
        return new DocumentService(
            context,
            _fileStorage,
            _notificationService,
            TestServiceFactory.CreateLogger<DocumentService>()
        );
    }

    [Fact]
    public async Task RecordDocumentAccessAsync_RecordsPreviewAndDownloadAuditLogs()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var service = CreateService(context);

        var doc = new Document
        {
            Title = "Annual Summary",
            Category = "Reports",
            OriginalFileName = "summary.pdf",
            StorageKey = "1/personal/summary.pdf",
            ContentType = "application/pdf",
            FileSize = 1024,
            UploadedByUserId = 1,
            CreatedDate = DateTime.UtcNow
        };
        context.Documents.Add(doc);
        await context.SaveChangesAsync();

        // Record Preview
        var previewResult = await service.RecordDocumentAccessAsync(doc.DocumentId, requestingUserId: 1, actionType: "Preview");
        Assert.True(previewResult);

        // Record Download
        var downloadResult = await service.RecordDocumentAccessAsync(doc.DocumentId, requestingUserId: 1, actionType: "Download");
        Assert.True(downloadResult);

        var logs = context.DocumentAuditLogs.Where(a => a.DocumentId == doc.DocumentId).ToList();
        Assert.Equal(2, logs.Count);
        Assert.Contains(logs, l => l.ActionType == "Preview" && l.UserId == 1);
        Assert.Contains(logs, l => l.ActionType == "Download" && l.UserId == 1);

        // Invalid action type throws ArgumentException
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.RecordDocumentAccessAsync(doc.DocumentId, requestingUserId: 1, actionType: "InvalidAction")
        );
    }

    [Fact]
    public async Task GetDocumentActivityReportAsync_EnforcesAdminAndAggregatesMetrics()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var service = CreateService(context);

        var now = DateTime.UtcNow;
        var doc1 = new Document
        {
            Title = "Doc 1",
            Category = "Reports",
            OriginalFileName = "doc1.pdf",
            StorageKey = "1/personal/doc1.pdf",
            ContentType = "application/pdf",
            FileSize = 1000,
            UploadedByUserId = 1,
            CreatedDate = now
        };

        var doc2 = new Document
        {
            Title = "Doc 2",
            Category = "Presentations",
            OriginalFileName = "doc2.pptx",
            StorageKey = "1/personal/doc2.pptx",
            ContentType = "application/vnd.ms-powerpoint",
            FileSize = 2000,
            UploadedByUserId = 1,
            CreatedDate = now
        };

        context.Documents.AddRange(doc1, doc2);
        await context.SaveChangesAsync();

        // Add some audit logs
        context.DocumentAuditLogs.AddRange(
            new DocumentAuditLog { DocumentId = doc1.DocumentId, UserId = 1, ActionType = "Download", Timestamp = now, Details = "Download 1" },
            new DocumentAuditLog { DocumentId = doc1.DocumentId, UserId = 1, ActionType = "Preview", Timestamp = now, Details = "Preview 1" }
        );
        await context.SaveChangesAsync();

        // Non-admin (User 1) is rejected
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.GetDocumentActivityReportAsync(null, null, requestingUserId: 1)
        );

        // Administrator (User 4) succeeds
        var today = DateOnly.FromDateTime(now);
        var report = await service.GetDocumentActivityReportAsync(today.AddDays(-1), today.AddDays(1), requestingUserId: 4);

        Assert.NotNull(report);
        Assert.Equal(2, report.TotalUploads);
        Assert.Equal(1, report.TotalDownloads);
        Assert.Equal(1, report.TotalPreviews);
        Assert.True(report.DocumentsByCategory.ContainsKey("Reports"));
        Assert.True(report.DocumentsByCategory.ContainsKey("Presentations"));
        Assert.Single(report.TopUploaders);
        Assert.Equal("Ni Kang", report.TopUploaders[0].DisplayName);
    }

    [Fact]
    public async Task DeleteDocumentAsync_WithAttachedTask_SafelyDetachesAndRecordsAudit()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var service = CreateService(context);

        var doc = new Document
        {
            Title = "Doc With Task",
            Category = "Project Documents",
            OriginalFileName = "attached.pdf",
            StorageKey = "1/10/attached.pdf",
            ContentType = "application/pdf",
            FileSize = 500,
            UploadedByUserId = 1,
            ProjectId = 10,
            TaskId = 100,
            CreatedDate = DateTime.UtcNow
        };
        context.Documents.Add(doc);
        await context.SaveChangesAsync();
        await _fileStorage.SaveFileAsync(new MemoryStream(new byte[500]), doc.StorageKey);

        var deleted = await service.DeleteDocumentAsync(doc.DocumentId, requestingUserId: 1);
        Assert.True(deleted);

        // Document record removed
        Assert.Null(await context.Documents.FindAsync(doc.DocumentId));
        Assert.False(await _fileStorage.FileExistsAsync(doc.StorageKey));

        // Audit log survives with ActionType = "Delete" and DocumentId = null
        var deleteAudit = context.DocumentAuditLogs.FirstOrDefault(a => a.ActionType == "Delete" && a.UserId == 1);
        Assert.NotNull(deleteAudit);
        Assert.Null(deleteAudit.DocumentId);
        Assert.Contains("Doc With Task", deleteAudit.Details);
    }
}
