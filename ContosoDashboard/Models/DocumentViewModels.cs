using System.ComponentModel.DataAnnotations;

namespace ContosoDashboard.Models;

public class DocumentUploadModel
{
    [Required(ErrorMessage = "Title is required")]
    [MaxLength(200, ErrorMessage = "Title cannot exceed 200 characters")]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000, ErrorMessage = "Description cannot exceed 1000 characters")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Category is required")]
    [MaxLength(50)]
    public string Category { get; set; } = "Project Documents";

    public int? ProjectId { get; set; }

    public int? TaskId { get; set; }

    [MaxLength(500)]
    public string? Tags { get; set; }

    public Stream? FileStream { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long FileSize { get; set; }
}

public class DocumentEditModel
{
    [Required(ErrorMessage = "Title is required")]
    [MaxLength(200, ErrorMessage = "Title cannot exceed 200 characters")]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Category is required")]
    [MaxLength(50)]
    public string Category { get; set; } = string.Empty;

    public int? ProjectId { get; set; }

    [MaxLength(500)]
    public string? Tags { get; set; }
}

public class DocumentSummaryStats
{
    public int TotalAccessibleDocuments { get; set; }
    public int PersonalDocumentsCount { get; set; }
    public int ProjectDocumentsCount { get; set; }
    public int SharedWithMeCount { get; set; }
}

public static class DocumentCategories
{
    public const string ProjectDocuments = "Project Documents";
    public const string TeamResources = "Team Resources";
    public const string PersonalFiles = "Personal Files";
    public const string Reports = "Reports";
    public const string Presentations = "Presentations";
    public const string Other = "Other";

    public static readonly IReadOnlyList<string> All = new[]
    {
        ProjectDocuments,
        TeamResources,
        PersonalFiles,
        Reports,
        Presentations,
        Other
    };
}

public class DocumentUploadResult
{
    public string OriginalFileName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int? DocumentId { get; set; }
    public long FileSize { get; set; }
}

public class DocumentActivityReport
{
    public DateOnly? StartDateUtc { get; set; }
    public DateOnly? EndDateUtc { get; set; }
    public int TotalUploads { get; set; }
    public int TotalDownloads { get; set; }
    public int TotalPreviews { get; set; }
    public Dictionary<string, int> DocumentsByCategory { get; set; } = new();
    public Dictionary<string, int> DocumentsByContentType { get; set; } = new();
    public List<UploaderActivityStat> TopUploaders { get; set; } = new();
    public List<ActivityPeriodStat> DailyActivityCounts { get; set; } = new();
}

public class UploaderActivityStat
{
    public int UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public int UploadCount { get; set; }
    public long TotalBytesUploaded { get; set; }
}

public class ActivityPeriodStat
{
    public string PeriodLabel { get; set; } = string.Empty;
    public int UploadCount { get; set; }
    public int DownloadCount { get; set; }
    public int PreviewCount { get; set; }
}

