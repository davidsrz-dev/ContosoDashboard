using System.Text;
using ContosoDashboard.Models;
using ContosoDashboard.Services;

namespace ContosoDashboard.Tests.Services;

public class DocumentAuthorizationTests
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
    public async Task AuthorizeAccessAsync_TeamLeadInSameDepartment_CanReadDocument()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var service = CreateService(context);

        // User 1 (Engineering Employee) creates a personal document
        var doc = new Document
        {
            Title = "Engineering Spec",
            Category = "Specifications",
            OriginalFileName = "spec.pdf",
            StorageKey = "1/personal/doc1.pdf",
            ContentType = "application/pdf",
            FileSize = 1024,
            UploadedByUserId = 1, // Engineering
            CreatedDate = DateTime.UtcNow
        };
        context.Documents.Add(doc);
        await context.SaveChangesAsync();

        // User 2 is TeamLead in Engineering
        var canRead = await service.AuthorizeAccessAsync(doc.DocumentId, requestingUserId: 2);
        Assert.True(canRead);
    }

    [Fact]
    public async Task Mutations_TeamLeadInSameDepartment_CannotEditReplaceOrDelete()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var service = CreateService(context);

        var doc = new Document
        {
            Title = "Original Title",
            Category = "Specifications",
            OriginalFileName = "spec.pdf",
            StorageKey = "1/personal/doc1.pdf",
            ContentType = "application/pdf",
            FileSize = 1024,
            UploadedByUserId = 1, // Engineering Employee
            CreatedDate = DateTime.UtcNow
        };
        context.Documents.Add(doc);
        await context.SaveChangesAsync();
        await _fileStorage.SaveFileAsync(new MemoryStream(new byte[100]), doc.StorageKey);

        // User 2 is TeamLead in Engineering (not owner, not PM, not Admin)
        var editResult = await service.UpdateDocumentMetadataAsync(
            doc.DocumentId,
            new DocumentEditModel { Title = "Modified by TeamLead", Category = "Specifications" },
            requestingUserId: 2
        );
        Assert.False(editResult);

        var replaceResult = await service.ReplaceDocumentFileAsync(
            doc.DocumentId,
            new MemoryStream(Encoding.UTF8.GetBytes("new content")),
            "new.pdf",
            "application/pdf",
            100,
            requestingUserId: 2
        );
        Assert.False(replaceResult);

        var deleteResult = await service.DeleteDocumentAsync(doc.DocumentId, requestingUserId: 2);
        Assert.False(deleteResult);

        // Verify document is still intact
        var unchangedDoc = await context.Documents.FindAsync(doc.DocumentId);
        Assert.NotNull(unchangedDoc);
        Assert.Equal("Original Title", unchangedDoc.Title);
    }

    [Fact]
    public async Task Mutations_ProjectManager_CanEditAndManageProjectDocument()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var service = CreateService(context);

        // Document uploaded to Project 10 by User 1 (Contributor)
        var doc = new Document
        {
            Title = "Project Plan",
            Category = "Project Documents",
            OriginalFileName = "plan.pdf",
            StorageKey = "1/10/plan.pdf",
            ContentType = "application/pdf",
            FileSize = 1024,
            UploadedByUserId = 1,
            ProjectId = 10, // ProjectManager is User 3
            CreatedDate = DateTime.UtcNow
        };
        context.Documents.Add(doc);
        await context.SaveChangesAsync();
        await _fileStorage.SaveFileAsync(new MemoryStream(new byte[100]), doc.StorageKey);

        // User 3 is Project Manager of Project 10
        var editResult = await service.UpdateDocumentMetadataAsync(
            doc.DocumentId,
            new DocumentEditModel { Title = "Updated by PM", Category = "Project Documents", ProjectId = 10 },
            requestingUserId: 3
        );
        Assert.True(editResult);

        var updatedDoc = await context.Documents.FindAsync(doc.DocumentId);
        Assert.NotNull(updatedDoc);
        Assert.Equal("Updated by PM", updatedDoc.Title);
    }

    [Fact]
    public async Task AuthorizeAccessAsync_NonProjectMember_DeniedAccess_PreventingIDOR()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var service = CreateService(context);

        // Document belongs to Project 10
        var doc = new Document
        {
            Title = "Internal Project Doc",
            Category = "Project Documents",
            OriginalFileName = "internal.pdf",
            StorageKey = "1/10/internal.pdf",
            ContentType = "application/pdf",
            FileSize = 1024,
            UploadedByUserId = 1,
            ProjectId = 10,
            CreatedDate = DateTime.UtcNow
        };
        context.Documents.Add(doc);
        await context.SaveChangesAsync();

        // User 5 is in Sales and is NOT a member of Project 10
        var canAccess = await service.AuthorizeAccessAsync(doc.DocumentId, requestingUserId: 5);
        Assert.False(canAccess);

        // Project document list should also be empty for User 5
        var projectDocs = await service.GetProjectDocumentsAsync(projectId: 10, requestingUserId: 5);
        Assert.Empty(projectDocs);
    }

    [Fact]
    public async Task Administrator_HasFullAccessToAnyDocument()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var service = CreateService(context);

        var doc = new Document
        {
            Title = "Private Employee Doc",
            Category = "Personal Files",
            OriginalFileName = "private.pdf",
            StorageKey = "1/personal/private.pdf",
            ContentType = "application/pdf",
            FileSize = 512,
            UploadedByUserId = 1,
            CreatedDate = DateTime.UtcNow
        };
        context.Documents.Add(doc);
        await context.SaveChangesAsync();
        await _fileStorage.SaveFileAsync(new MemoryStream(new byte[512]), doc.StorageKey);

        // User 4 is Administrator
        var canAccess = await service.AuthorizeAccessAsync(doc.DocumentId, requestingUserId: 4);
        Assert.True(canAccess);

        var canEdit = await service.UpdateDocumentMetadataAsync(
            doc.DocumentId,
            new DocumentEditModel { Title = "Admin Modified", Category = "Personal Files" },
            requestingUserId: 4
        );
        Assert.True(canEdit);

        var canDelete = await service.DeleteDocumentAsync(doc.DocumentId, requestingUserId: 4);
        Assert.True(canDelete);

        Assert.Null(await context.Documents.FindAsync(doc.DocumentId));
    }
}
