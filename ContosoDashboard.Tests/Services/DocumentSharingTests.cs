using System.Text;
using ContosoDashboard.Models;
using ContosoDashboard.Services;

namespace ContosoDashboard.Tests.Services;

public class DocumentSharingTests
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
    public async Task ShareDocumentWithDepartment_EvaluatesDynamicallyBasedOnUserCurrentDepartment()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var service = CreateService(context);

        // User 1 (Engineering) creates a doc and shares with "Sales"
        var doc = new Document
        {
            Title = "Sales Playbook",
            Category = "Team Resources",
            OriginalFileName = "playbook.pdf",
            StorageKey = "1/personal/playbook.pdf",
            ContentType = "application/pdf",
            FileSize = 1024,
            UploadedByUserId = 1,
            CreatedDate = DateTime.UtcNow
        };
        context.Documents.Add(doc);
        await context.SaveChangesAsync();

        var shared = await service.ShareDocumentWithDepartmentAsync(doc.DocumentId, "Sales", requestingUserId: 1);
        Assert.True(shared);

        // User 5 currently has Department = "Sales"
        var user5Docs = await service.GetSharedDocumentsAsync(requestingUserId: 5);
        Assert.Single(user5Docs);
        Assert.Equal("Sales Playbook", user5Docs[0].Title);

        var canAccess = await service.AuthorizeAccessAsync(doc.DocumentId, requestingUserId: 5);
        Assert.True(canAccess);

        // Dynamically transfer User 5 from "Sales" to "Marketing"
        var user5 = await context.Users.FindAsync(5);
        Assert.NotNull(user5);
        user5.Department = "Marketing";
        await context.SaveChangesAsync();

        // User 5 should no longer see or have access to the document
        var user5DocsAfterTransfer = await service.GetSharedDocumentsAsync(requestingUserId: 5);
        Assert.Empty(user5DocsAfterTransfer);

        var canAccessAfterTransfer = await service.AuthorizeAccessAsync(doc.DocumentId, requestingUserId: 5);
        Assert.False(canAccessAfterTransfer);
    }

    [Fact]
    public async Task UploadDocumentAsync_ToProject_NotifiesCollaboratorsAndExcludesAuthor()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var service = CreateService(context);

        // Project 10 has ProjectManager (User 3) and Member (User 1)
        var content = Encoding.UTF8.GetBytes("Project Sprint Specs");
        var uploadModel = new DocumentUploadModel
        {
            Title = "Sprint Specs",
            Category = "Project Documents",
            ProjectId = 10,
            FileName = "sprint.pdf",
            ContentType = "application/pdf",
            FileSize = content.Length,
            FileStream = new MemoryStream(content)
        };

        // User 1 uploads to Project 10
        var doc = await service.UploadDocumentAsync(uploadModel, requestingUserId: 1);
        Assert.NotNull(doc);

        // User 3 (Project Manager) should receive a notification
        var pmNotifications = _notificationService.Notifications.Where(n => n.UserId == 3).ToList();
        Assert.Single(pmNotifications);
        Assert.Contains("Sprint Specs", pmNotifications[0].Message);

        // User 1 (Author) should NOT receive any notification
        var authorNotifications = _notificationService.Notifications.Where(n => n.UserId == 1).ToList();
        Assert.Empty(authorNotifications);
    }
}
