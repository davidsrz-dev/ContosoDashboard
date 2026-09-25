using System.Text;
using ContosoDashboard.Models;
using ContosoDashboard.Services;

namespace ContosoDashboard.Tests.Services;

public class DocumentTaskTests
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
    public async Task UploadTaskDocumentAsync_ValidTask_AutoAssociatesProjectAndLogsAudit()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var service = CreateService(context);

        // Task 100 belongs to Project 10 and is assigned to User 1
        var content = Encoding.UTF8.GetBytes("Task attachment content");
        var uploadModel = new DocumentUploadModel
        {
            Title = "Task Deliverable",
            Category = "Project Documents",
            FileName = "deliverable.pdf",
            ContentType = "application/pdf",
            FileSize = content.Length,
            FileStream = new MemoryStream(content)
        };

        var doc = await service.UploadTaskDocumentAsync(taskId: 100, uploadModel, requestingUserId: 1);

        Assert.NotNull(doc);
        Assert.Equal(100, doc.TaskId);
        Assert.Equal(10, doc.ProjectId); // Auto-derived from Task 100's ProjectId
        Assert.Equal("Task Deliverable", doc.Title);

        // Check audit logs: should contain both Upload and AttachToTask
        var auditLogs = context.DocumentAuditLogs.Where(a => a.DocumentId == doc.DocumentId).ToList();
        Assert.Equal(2, auditLogs.Count);
        Assert.Contains(auditLogs, a => a.ActionType == "Upload");
        Assert.Contains(auditLogs, a => a.ActionType == "AttachToTask");

        // Verify task document list includes it
        var taskDocs = await service.GetTaskDocumentsAsync(taskId: 100, requestingUserId: 1);
        Assert.Single(taskDocs);
        Assert.Equal("Task Deliverable", taskDocs[0].Title);
    }

    [Fact]
    public async Task UploadTaskDocumentAsync_UnauthorizedUser_ThrowsUnauthorizedAccessException()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var service = CreateService(context);

        // User 5 (Sales) is NOT assigned to Task 100 and NOT in Project 10
        var content = Encoding.UTF8.GetBytes("Unauthorized upload");
        var uploadModel = new DocumentUploadModel
        {
            Title = "Sneaky Doc",
            Category = "Project Documents",
            FileName = "doc.pdf",
            ContentType = "application/pdf",
            FileSize = content.Length,
            FileStream = new MemoryStream(content)
        };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.UploadTaskDocumentAsync(taskId: 100, uploadModel, requestingUserId: 5)
        );
    }

    [Fact]
    public async Task DetachDocumentFromTaskAsync_DetachesReferenceAndLogsAudit()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var service = CreateService(context);

        var doc = new Document
        {
            Title = "Attached Doc",
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

        var detached = await service.DetachDocumentFromTaskAsync(doc.DocumentId, taskId: 100, requestingUserId: 1);
        Assert.True(detached);

        var updatedDoc = await context.Documents.FindAsync(doc.DocumentId);
        Assert.NotNull(updatedDoc);
        Assert.Null(updatedDoc.TaskId);

        // Check DetachFromTask audit log
        var audit = context.DocumentAuditLogs.FirstOrDefault(a => a.DocumentId == doc.DocumentId && a.ActionType == "DetachFromTask");
        Assert.NotNull(audit);
    }
}
