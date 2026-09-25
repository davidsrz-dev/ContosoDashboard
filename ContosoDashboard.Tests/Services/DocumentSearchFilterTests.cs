using ContosoDashboard.Models;
using ContosoDashboard.Services;

namespace ContosoDashboard.Tests.Services;

public class DocumentSearchFilterTests
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
    public async Task SearchDocumentsAsync_InclusiveUtcDateRange_FiltersCorrectly()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var service = CreateService(context);

        // Document 1: created 2026-09-10 14:30 UTC
        var doc1 = new Document
        {
            Title = "Doc September 10",
            Category = "Reports",
            OriginalFileName = "sep10.pdf",
            StorageKey = "1/personal/sep10.pdf",
            ContentType = "application/pdf",
            FileSize = 1000,
            UploadedByUserId = 1,
            CreatedDate = new DateTime(2026, 9, 10, 14, 30, 0, DateTimeKind.Utc)
        };

        // Document 2: created 2026-09-15 08:00 UTC
        var doc2 = new Document
        {
            Title = "Doc September 15",
            Category = "Reports",
            OriginalFileName = "sep15.pdf",
            StorageKey = "1/personal/sep15.pdf",
            ContentType = "application/pdf",
            FileSize = 1000,
            UploadedByUserId = 1,
            CreatedDate = new DateTime(2026, 9, 15, 8, 0, 0, DateTimeKind.Utc)
        };

        // Document 3: created 2026-09-20 23:59 UTC
        var doc3 = new Document
        {
            Title = "Doc September 20",
            Category = "Reports",
            OriginalFileName = "sep20.pdf",
            StorageKey = "1/personal/sep20.pdf",
            ContentType = "application/pdf",
            FileSize = 1000,
            UploadedByUserId = 1,
            CreatedDate = new DateTime(2026, 9, 20, 23, 59, 59, DateTimeKind.Utc)
        };

        context.Documents.AddRange(doc1, doc2, doc3);
        await context.SaveChangesAsync();

        // 1. Same-day inclusive query: 2026-09-10 to 2026-09-10 should return doc1
        var exactDayResults = await service.SearchDocumentsAsync(
            searchTerm: "",
            category: null,
            projectId: null,
            startDateUtc: new DateOnly(2026, 9, 10),
            endDateUtc: new DateOnly(2026, 9, 10),
            requestingUserId: 1
        );
        Assert.Single(exactDayResults);
        Assert.Equal("Doc September 10", exactDayResults[0].Title);

        // 2. Range query: 2026-09-10 to 2026-09-15 should return doc1 and doc2
        var rangeResults = await service.SearchDocumentsAsync(
            searchTerm: "",
            category: null,
            projectId: null,
            startDateUtc: new DateOnly(2026, 9, 10),
            endDateUtc: new DateOnly(2026, 9, 15),
            requestingUserId: 1
        );
        Assert.Equal(2, rangeResults.Count);
        Assert.Contains(rangeResults, d => d.Title == "Doc September 10");
        Assert.Contains(rangeResults, d => d.Title == "Doc September 15");

        // 3. Out of bounds query: 2026-09-11 to 2026-09-14 should return empty
        var emptyResults = await service.SearchDocumentsAsync(
            searchTerm: "",
            category: null,
            projectId: null,
            startDateUtc: new DateOnly(2026, 9, 11),
            endDateUtc: new DateOnly(2026, 9, 14),
            requestingUserId: 1
        );
        Assert.Empty(emptyResults);
    }

    [Fact]
    public async Task SearchDocumentsAsync_SearchesByAuthorAndProjectName()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var service = CreateService(context);

        // Document in Project 10 (Project Alpha) uploaded by User 1 (Ni Kang)
        var projectDoc = new Document
        {
            Title = "Architecture Blueprint",
            Category = "Project Documents",
            OriginalFileName = "arch.pdf",
            StorageKey = "1/10/arch.pdf",
            ContentType = "application/pdf",
            FileSize = 2048,
            UploadedByUserId = 1, // DisplayName: "Ni Kang"
            ProjectId = 10,        // Name: "Project Alpha"
            CreatedDate = DateTime.UtcNow
        };

        context.Documents.Add(projectDoc);
        await context.SaveChangesAsync();

        // Search by author name "Ni Kang"
        var authorResults = await service.SearchDocumentsAsync(
            searchTerm: "Ni Kang",
            category: null,
            projectId: null,
            startDateUtc: null,
            endDateUtc: null,
            requestingUserId: 1
        );
        Assert.Single(authorResults);
        Assert.Equal("Architecture Blueprint", authorResults[0].Title);

        // Search by project name "Project Alpha"
        var projectResults = await service.SearchDocumentsAsync(
            searchTerm: "Alpha",
            category: null,
            projectId: null,
            startDateUtc: null,
            endDateUtc: null,
            requestingUserId: 1
        );
        Assert.Single(projectResults);
        Assert.Equal("Architecture Blueprint", projectResults[0].Title);
    }

    [Fact]
    public async Task CheckDuplicateTitleAsync_DetectsExistingTitleUnderSameCategoryAndProject()
    {
        using var context = TestDbContextFactory.CreateInMemoryContext();
        var service = CreateService(context);

        var doc = new Document
        {
            Title = "Quarterly Financials",
            Category = "Financial",
            OriginalFileName = "q1.xlsx",
            StorageKey = "1/10/q1.xlsx",
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            FileSize = 1024,
            UploadedByUserId = 1,
            ProjectId = 10,
            CreatedDate = DateTime.UtcNow
        };
        context.Documents.Add(doc);
        await context.SaveChangesAsync();

        // Case-insensitive match in same project and category
        var existsExact = await service.CheckDuplicateTitleAsync("Quarterly Financials", "Financial", 10);
        Assert.True(existsExact);

        var existsLower = await service.CheckDuplicateTitleAsync("quarterly financials", "Financial", 10);
        Assert.True(existsLower);

        // Different category -> false
        var existsDiffCategory = await service.CheckDuplicateTitleAsync("Quarterly Financials", "Marketing", 10);
        Assert.False(existsDiffCategory);

        // Different project -> false
        var existsDiffProject = await service.CheckDuplicateTitleAsync("Quarterly Financials", "Financial", 20);
        Assert.False(existsDiffProject);
    }
}
