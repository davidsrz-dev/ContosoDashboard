using ContosoDashboard.Models;
using ContosoDashboard.Services;

namespace ContosoDashboard.Tests;

public class DocumentSecurityAndIntegrityTests
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

    [Theory]
    [InlineData("test.pdf", "", "El tipo de contenido (MIME) es obligatorio.")]
    [InlineData("test.pdf", "   ", "El tipo de contenido (MIME) es obligatorio.")]
    [InlineData("test.pdf", "application/octet-stream", "El tipo MIME genérico 'application/octet-stream' no está permitido.")]
    [InlineData("test.png", "application/octet-stream", "El tipo MIME genérico 'application/octet-stream' no está permitido.")]
    public void ValidateFile_RejectsEmptyOrOctetStreamMime(string fileName, string contentType, string expectedErrorSubstring)
    {
        var result = DocumentService.ValidateFile(fileName, contentType, 1024);
        Assert.False(result.IsValid);
        Assert.Contains(expectedErrorSubstring, result.ErrorMessage);
    }

    [Fact]
    public async Task AttachDocumentToTask_UnauthorizedUser_ReturnsFalse()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var service = CreateService(context);

        // Document owned by User 1
        var doc = new Document
        {
            Title = "Doc 1",
            OriginalFileName = "doc1.pdf",
            StorageKey = "1/personal/doc1.pdf",
            ContentType = "application/pdf",
            FileSize = 500,
            Category = "Personal Files",
            UploadedByUserId = 1,
            CreatedDate = DateTime.UtcNow
        };
        context.Documents.Add(doc);
        await context.SaveChangesAsync();

        // User 5 (Sales, not a member of Project 10, not assigned to Task 100, not owner of doc) tries to attach
        var attached = await service.AttachDocumentToTaskAsync(doc.DocumentId, taskId: 100, requestingUserId: 5);

        Assert.False(attached);
        Assert.Null(doc.TaskId);
    }

    [Fact]
    public async Task DetachDocumentFromTask_UnauthorizedUser_ReturnsFalse()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var service = CreateService(context);

        // Document attached to Task 100, owned by User 1
        var doc = new Document
        {
            Title = "Doc 1",
            OriginalFileName = "doc1.pdf",
            StorageKey = "1/10/doc1.pdf",
            ContentType = "application/pdf",
            FileSize = 500,
            Category = "Project Documents",
            UploadedByUserId = 1,
            ProjectId = 10,
            TaskId = 100,
            CreatedDate = DateTime.UtcNow
        };
        context.Documents.Add(doc);
        await context.SaveChangesAsync();

        // User 5 (unauthorized) attempts to detach doc from Task 100
        var detached = await service.DetachDocumentFromTaskAsync(doc.DocumentId, taskId: 100, requestingUserId: 5);

        Assert.False(detached);
        Assert.Equal(100, doc.TaskId);
    }

    [Fact]
    public async Task UpdateDocumentMetadata_UnauthorizedTargetProject_ReturnsFalse()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var service = CreateService(context);

        // Add a secret project where User 1 is NOT a member
        var secretProject = new Project
        {
            ProjectId = 99,
            Name = "Secret Project",
            ProjectManagerId = 3,
            Status = ProjectStatus.Active,
            StartDate = DateTime.UtcNow
        };
        context.Projects.Add(secretProject);

        var doc = new Document
        {
            Title = "Personal Notes",
            OriginalFileName = "notes.pdf",
            StorageKey = "1/personal/notes.pdf",
            ContentType = "application/pdf",
            FileSize = 200,
            Category = "Personal Files",
            UploadedByUserId = 1,
            CreatedDate = DateTime.UtcNow
        };
        context.Documents.Add(doc);
        await context.SaveChangesAsync();

        // User 1 tries to move document to Project 99 (where they have no access)
        var editModel = new DocumentEditModel
        {
            Title = "Personal Notes Updated",
            Category = "Personal Files",
            ProjectId = 99
        };

        var updated = await service.UpdateDocumentMetadataAsync(doc.DocumentId, editModel, requestingUserId: 1);

        Assert.False(updated);
        Assert.Null(doc.ProjectId);
    }

    [Fact]
    public async Task TeamLead_DepartmentDocuments_IncludedInStatsAndQuery()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var service = CreateService(context);

        // User 1 is Engineering, User 2 is TeamLead of Engineering
        var doc = new Document
        {
            Title = "Engineering Specs",
            OriginalFileName = "specs.pdf",
            StorageKey = "1/personal/specs.pdf",
            ContentType = "application/pdf",
            FileSize = 1024,
            Category = "Team Resources",
            UploadedByUserId = 1,
            CreatedDate = DateTime.UtcNow
        };
        context.Documents.Add(doc);
        await context.SaveChangesAsync();

        // Team Lead queries department documents
        var deptDocs = await service.GetDepartmentDocumentsAsync(requestingUserId: 2);
        Assert.Single(deptDocs);
        Assert.Equal("Engineering Specs", deptDocs[0].Title);

        // Stats should include department documents in total accessible count
        var stats = await service.GetDocumentStatsAsync(requestingUserId: 2);
        Assert.True(stats.TotalAccessibleDocuments >= 1);
    }
}