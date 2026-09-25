using System.Text;
using ContosoDashboard.Models;
using ContosoDashboard.Services;

namespace ContosoDashboard.Tests.Services;

public class DocumentUploadTests
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
    public async Task UploadDocumentsAsync_SingleValidFile_SucceedsAndStoresAtomically()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var service = CreateService(context);

        var content = Encoding.UTF8.GetBytes("Test PDF content");
        var uploadModel = new DocumentUploadModel
        {
            Title = "Valid Document",
            Category = "Personal Files",
            FileName = "sample.pdf",
            ContentType = "application/pdf",
            FileSize = content.Length,
            FileStream = new MemoryStream(content)
        };

        var results = await service.UploadDocumentsAsync(new[] { uploadModel }, requestingUserId: 1);

        Assert.Single(results);
        var result = results[0];
        Assert.True(result.Success);
        Assert.NotNull(result.DocumentId);
        Assert.Equal("sample.pdf", result.OriginalFileName);

        // Verify database entity
        var doc = await context.Documents.FindAsync(result.DocumentId.Value);
        Assert.NotNull(doc);
        Assert.Equal("Valid Document", doc.Title);
        Assert.Equal(1, doc.UploadedByUserId);
        Assert.True(await _fileStorage.FileExistsAsync(doc.StorageKey));
    }

    [Fact]
    public async Task UploadDocumentsAsync_FileExceeds25MB_FailsGracefullyWithoutSaving()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var service = CreateService(context);

        const long oversized = 26214401; // > 25 MB
        var uploadModel = new DocumentUploadModel
        {
            Title = "Oversized Document",
            Category = "Reports",
            FileName = "large.pdf",
            ContentType = "application/pdf",
            FileSize = oversized,
            FileStream = new MemoryStream(new byte[10])
        };

        var results = await service.UploadDocumentsAsync(new[] { uploadModel }, requestingUserId: 1);

        Assert.Single(results);
        var result = results[0];
        Assert.False(result.Success);
        Assert.Contains("25 MB", result.ErrorMessage);
        Assert.Null(result.DocumentId);

        // Verify zero database records and zero files stored
        Assert.Empty(context.Documents);
        Assert.Empty(_fileStorage.Files);
    }

    [Fact]
    public async Task UploadDocumentsAsync_DisallowedExtension_FailsGracefully()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var service = CreateService(context);

        var uploadModel = new DocumentUploadModel
        {
            Title = "Malicious file",
            Category = "Other",
            FileName = "script.exe",
            ContentType = "application/octet-stream",
            FileSize = 500,
            FileStream = new MemoryStream(new byte[500])
        };

        var results = await service.UploadDocumentsAsync(new[] { uploadModel }, requestingUserId: 1);

        Assert.Single(results);
        Assert.False(results[0].Success);
        Assert.Contains("no está permitido", results[0].ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(context.Documents);
        Assert.Empty(_fileStorage.Files);
    }

    [Fact]
    public async Task UploadDocumentsAsync_BatchWithMixedValidAndInvalid_ProcessesIndependently()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var service = CreateService(context);

        var file1Content = Encoding.UTF8.GetBytes("File 1 PDF");
        var file3Content = Encoding.UTF8.GetBytes("File 3 PNG");

        var batch = new List<DocumentUploadModel>
        {
            new()
            {
                Title = "Doc 1 Valid",
                Category = "Personal Files",
                FileName = "doc1.pdf",
                ContentType = "application/pdf",
                FileSize = file1Content.Length,
                FileStream = new MemoryStream(file1Content)
            },
            new()
            {
                Title = "Doc 2 Invalid",
                Category = "Personal Files",
                FileName = "archive.zip",
                ContentType = "application/zip",
                FileSize = 1000,
                FileStream = new MemoryStream(new byte[1000])
            },
            new()
            {
                Title = "Doc 3 Valid",
                Category = "Personal Files",
                FileName = "image.png",
                ContentType = "image/png",
                FileSize = file3Content.Length,
                FileStream = new MemoryStream(file3Content)
            }
        };

        var results = await service.UploadDocumentsAsync(batch, requestingUserId: 1);

        Assert.Equal(3, results.Count);
        Assert.True(results[0].Success);
        Assert.False(results[1].Success);
        Assert.True(results[2].Success);

        // Only 2 documents should be in DB and storage
        Assert.Equal(2, context.Documents.Count());
        Assert.Equal(2, _fileStorage.Files.Count);
    }
}
