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
    public static readonly IReadOnlyList<string> All = new[]
    {
        "Project Documents",
        "Team Resources",
        "Personal Files",
        "Reports",
        "Presentations",
        "Other"
    };
}
