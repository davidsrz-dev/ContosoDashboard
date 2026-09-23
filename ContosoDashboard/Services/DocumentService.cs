using Microsoft.EntityFrameworkCore;
using ContosoDashboard.Data;
using ContosoDashboard.Models;

namespace ContosoDashboard.Services;

public class DocumentService : IDocumentService
{
    private readonly ApplicationDbContext _context;
    private readonly IFileStorageService _fileStorageService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<DocumentService> _logger;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".jpg", ".jpeg", ".png"
    };

    private const long MaxFileSizeInBytes = 26214400; // 25 MB

    public DocumentService(
        ApplicationDbContext context,
        IFileStorageService fileStorageService,
        INotificationService notificationService,
        ILogger<DocumentService> logger)
    {
        _context = context;
        _fileStorageService = fileStorageService;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<bool> AuthorizeAccessAsync(int documentId, int requestingUserId)
    {
        var document = await _context.Documents
            .Include(d => d.Project)
                .ThenInclude(p => p!.ProjectMembers)
            .Include(d => d.Shares)
            .Include(d => d.UploadedByUser)
            .FirstOrDefaultAsync(d => d.DocumentId == documentId);

        if (document == null) return false;

        var requestingUser = await _context.Users.FindAsync(requestingUserId);
        if (requestingUser == null) return false;

        // Admin has full access
        if (requestingUser.Role == UserRole.Administrator) return true;

        // Owner has full access
        if (document.UploadedByUserId == requestingUserId) return true;

        // Project members and project managers
        if (document.ProjectId.HasValue && document.Project != null)
        {
            if (document.Project.ProjectManagerId == requestingUserId) return true;
            if (document.Project.ProjectMembers.Any(pm => pm.UserId == requestingUserId)) return true;
        }

        // Shared directly with user
        if (document.Shares.Any(s => s.SharedWithUserId == requestingUserId)) return true;

        // Shared with user's current department (Dynamic evaluation per FR-017)
        if (!string.IsNullOrEmpty(requestingUser.Department) &&
            document.Shares.Any(s => !string.IsNullOrEmpty(s.SharedWithDepartment) &&
                                     s.SharedWithDepartment.Equals(requestingUser.Department, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        // Team Leads can view documents of members in their department
        if (requestingUser.Role == UserRole.TeamLead &&
            document.UploadedByUser != null &&
            !string.IsNullOrEmpty(document.UploadedByUser.Department) &&
            document.UploadedByUser.Department.Equals(requestingUser.Department, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    public async Task<List<Document>> GetUserDocumentsAsync(int requestingUserId)
    {
        return await _context.Documents
            .Include(d => d.Project)
            .Include(d => d.Task)
            .Include(d => d.UploadedByUser)
            .Where(d => d.UploadedByUserId == requestingUserId)
            .OrderByDescending(d => d.CreatedDate)
            .ToListAsync();
    }

    public async Task<List<Document>> GetProjectDocumentsAsync(int projectId, int requestingUserId)
    {
        var project = await _context.Projects
            .Include(p => p.ProjectMembers)
            .FirstOrDefaultAsync(p => p.ProjectId == projectId);

        if (project == null) return new List<Document>();

        var user = await _context.Users.FindAsync(requestingUserId);
        if (user == null) return new List<Document>();

        var isAuthorized = user.Role == UserRole.Administrator ||
                           project.ProjectManagerId == requestingUserId ||
                           project.ProjectMembers.Any(pm => pm.UserId == requestingUserId);

        if (!isAuthorized) return new List<Document>();

        return await _context.Documents
            .Include(d => d.Project)
            .Include(d => d.Task)
            .Include(d => d.UploadedByUser)
            .Where(d => d.ProjectId == projectId)
            .OrderByDescending(d => d.CreatedDate)
            .ToListAsync();
    }

    public async Task<List<Document>> GetSharedDocumentsAsync(int requestingUserId)
    {
        var user = await _context.Users.FindAsync(requestingUserId);
        if (user == null) return new List<Document>();

        var userDept = user.Department ?? string.Empty;

        return await _context.Documents
            .Include(d => d.UploadedByUser)
            .Include(d => d.Project)
            .Include(d => d.Shares)
                .ThenInclude(s => s.SharedByUser)
            .Where(d => d.UploadedByUserId != requestingUserId &&
                        d.Shares.Any(s => s.SharedWithUserId == requestingUserId ||
                                         (!string.IsNullOrEmpty(s.SharedWithDepartment) && s.SharedWithDepartment == userDept)))
            .OrderByDescending(d => d.CreatedDate)
            .ToListAsync();
    }

    public async Task<List<Document>> SearchDocumentsAsync(string searchTerm, string? category, int? projectId, int requestingUserId)
    {
        var user = await _context.Users.FindAsync(requestingUserId);
        if (user == null) return new List<Document>();

        var userDept = user.Department ?? string.Empty;
        var isAdmin = user.Role == UserRole.Administrator;

        var query = _context.Documents
            .Include(d => d.UploadedByUser)
            .Include(d => d.Project)
                .ThenInclude(p => p!.ProjectMembers)
            .Include(d => d.Shares)
            .AsQueryable();

        // Security filter: User can only see authorized documents
        if (!isAdmin)
        {
            query = query.Where(d =>
                d.UploadedByUserId == requestingUserId ||
                (d.ProjectId.HasValue && (d.Project!.ProjectManagerId == requestingUserId || d.Project.ProjectMembers.Any(pm => pm.UserId == requestingUserId))) ||
                d.Shares.Any(s => s.SharedWithUserId == requestingUserId || (!string.IsNullOrEmpty(s.SharedWithDepartment) && s.SharedWithDepartment == userDept)) ||
                (user.Role == UserRole.TeamLead && d.UploadedByUser.Department == userDept)
            );
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(d => d.Category == category);
        }

        if (projectId.HasValue)
        {
            query = query.Where(d => d.ProjectId == projectId.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            query = query.Where(d =>
                d.Title.Contains(term) ||
                (d.Description != null && d.Description.Contains(term)) ||
                (d.Tags != null && d.Tags.Contains(term)) ||
                d.OriginalFileName.Contains(term) ||
                (d.Project != null && d.Project.Name.Contains(term)) ||
                d.UploadedByUser.DisplayName.Contains(term)
            );
        }

        return await query.OrderByDescending(d => d.CreatedDate).ToListAsync();
    }

    public async Task<Document?> GetDocumentByIdAsync(int documentId, int requestingUserId)
    {
        var isAuthorized = await AuthorizeAccessAsync(documentId, requestingUserId);
        if (!isAuthorized) return null;

        return await _context.Documents
            .Include(d => d.UploadedByUser)
            .Include(d => d.Project)
                .ThenInclude(p => p!.ProjectMembers)
            .Include(d => d.Task)
            .Include(d => d.Shares)
                .ThenInclude(s => s.SharedWithUser)
            .Include(d => d.Shares)
                .ThenInclude(s => s.SharedByUser)
            .FirstOrDefaultAsync(d => d.DocumentId == documentId);
    }

    public async Task<Document> UploadDocumentAsync(DocumentUploadModel model, int requestingUserId)
    {
        if (model.FileStream == null)
            throw new ArgumentException("File content cannot be empty.");

        if (model.FileSize > MaxFileSizeInBytes)
            throw new InvalidOperationException($"File exceeds maximum allowed size of 25 MB ({MaxFileSizeInBytes} bytes).");

        var extension = Path.GetExtension(model.FileName);
        if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
            throw new InvalidOperationException($"File type '{extension}' is not supported. Supported extensions: {string.Join(", ", AllowedExtensions)}");

        var user = await _context.Users.FindAsync(requestingUserId)
                   ?? throw new UnauthorizedAccessException("Requesting user does not exist.");

        // If project specified, verify user has membership or admin privileges
        if (model.ProjectId.HasValue)
        {
            var project = await _context.Projects
                .Include(p => p.ProjectMembers)
                .FirstOrDefaultAsync(p => p.ProjectId == model.ProjectId.Value);

            if (project == null) throw new ArgumentException("Specified project does not exist.");

            var canUploadToProject = user.Role == UserRole.Administrator ||
                                     project.ProjectManagerId == requestingUserId ||
                                     project.ProjectMembers.Any(pm => pm.UserId == requestingUserId);

            if (!canUploadToProject)
                throw new UnauthorizedAccessException("You are not a member or manager of the specified project.");
        }

        // Generate unique GUID storage key outside wwwroot
        var projectFolder = model.ProjectId.HasValue ? model.ProjectId.Value.ToString() : "personal";
        var storageKey = $"{requestingUserId}/{projectFolder}/{Guid.NewGuid()}{extension}";

        // Atomic sequence: 1. Save file to disk
        await _fileStorageService.SaveFileAsync(model.FileStream, storageKey);

        var document = new Document
        {
            Title = model.Title.Trim(),
            Description = model.Description?.Trim(),
            Category = model.Category,
            OriginalFileName = Path.GetFileName(model.FileName),
            StorageKey = storageKey,
            FileSize = model.FileSize,
            ContentType = string.IsNullOrWhiteSpace(model.ContentType) ? "application/octet-stream" : model.ContentType,
            UploadedByUserId = requestingUserId,
            ProjectId = model.ProjectId,
            TaskId = model.TaskId,
            Tags = model.Tags?.Trim(),
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };

        try
        {
            _context.Documents.Add(document);

            // Audit Log
            _context.DocumentAuditLogs.Add(new DocumentAuditLog
            {
                Document = document,
                UserId = requestingUserId,
                ActionType = "Upload",
                Timestamp = DateTime.UtcNow,
                Details = $"Uploaded '{document.OriginalFileName}' ({document.FileSize} bytes) to category '{document.Category}'."
            });

            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database insert failed for uploaded document. Purging physical file {StorageKey}", storageKey);
            await _fileStorageService.DeleteFileAsync(storageKey);
            throw;
        }

        // Send notifications if uploaded to project (excluding the author per FR-018 clarification)
        if (document.ProjectId.HasValue)
        {
            var projectMembers = await _context.ProjectMembers
                .Where(pm => pm.ProjectId == document.ProjectId.Value && pm.UserId != requestingUserId)
                .Select(pm => pm.UserId)
                .ToListAsync();

            var project = await _context.Projects.FindAsync(document.ProjectId.Value);
            if (project != null && project.ProjectManagerId != requestingUserId && !projectMembers.Contains(project.ProjectManagerId))
            {
                projectMembers.Add(project.ProjectManagerId);
            }

            foreach (var recipientUserId in projectMembers)
            {
                await _notificationService.CreateNotificationAsync(new Notification
                {
                    UserId = recipientUserId,
                    Title = "New Project Document",
                    Message = $"{user.DisplayName} uploaded '{document.Title}' to project '{project?.Name}'.",
                    Type = NotificationType.SystemAlert,
                    Priority = NotificationPriority.Normal
                });
            }
        }

        return document;
    }

    public async Task<bool> UpdateDocumentMetadataAsync(int documentId, DocumentEditModel model, int requestingUserId)
    {
        var document = await _context.Documents
            .Include(d => d.Project)
            .FirstOrDefaultAsync(d => d.DocumentId == documentId);

        if (document == null) return false;

        var user = await _context.Users.FindAsync(requestingUserId);
        if (user == null) return false;

        var canEdit = user.Role == UserRole.Administrator ||
                      document.UploadedByUserId == requestingUserId ||
                      (document.ProjectId.HasValue && document.Project != null && document.Project.ProjectManagerId == requestingUserId);

        if (!canEdit) return false;

        document.Title = model.Title.Trim();
        document.Description = model.Description?.Trim();
        document.Category = model.Category;
        document.ProjectId = model.ProjectId;
        document.Tags = model.Tags?.Trim();
        document.UpdatedDate = DateTime.UtcNow;

        _context.DocumentAuditLogs.Add(new DocumentAuditLog
        {
            DocumentId = document.DocumentId,
            UserId = requestingUserId,
            ActionType = "EditMetadata",
            Timestamp = DateTime.UtcNow,
            Details = $"Updated metadata for document '{document.Title}'."
        });

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ReplaceDocumentFileAsync(int documentId, Stream newFileStream, string newFileName, string contentType, long fileSize, int requestingUserId)
    {
        var document = await _context.Documents
            .Include(d => d.Project)
            .FirstOrDefaultAsync(d => d.DocumentId == documentId);

        if (document == null) return false;

        var user = await _context.Users.FindAsync(requestingUserId);
        if (user == null) return false;

        var canEdit = user.Role == UserRole.Administrator ||
                      document.UploadedByUserId == requestingUserId ||
                      (document.ProjectId.HasValue && document.Project != null && document.Project.ProjectManagerId == requestingUserId);

        if (!canEdit) return false;

        if (fileSize > MaxFileSizeInBytes)
            throw new InvalidOperationException("File exceeds maximum allowed size of 25 MB.");

        var extension = Path.GetExtension(newFileName);
        if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
            throw new InvalidOperationException($"File type '{extension}' is not supported.");

        var oldStorageKey = document.StorageKey;
        var projectFolder = document.ProjectId.HasValue ? document.ProjectId.Value.ToString() : "personal";
        var newStorageKey = $"{document.UploadedByUserId}/{projectFolder}/{Guid.NewGuid()}{extension}";

        await _fileStorageService.SaveFileAsync(newFileStream, newStorageKey);

        document.StorageKey = newStorageKey;
        document.OriginalFileName = Path.GetFileName(newFileName);
        document.ContentType = contentType;
        document.FileSize = fileSize;
        document.UpdatedDate = DateTime.UtcNow;

        _context.DocumentAuditLogs.Add(new DocumentAuditLog
        {
            DocumentId = document.DocumentId,
            UserId = requestingUserId,
            ActionType = "ReplaceFile",
            Timestamp = DateTime.UtcNow,
            Details = $"Replaced physical file with '{document.OriginalFileName}' ({fileSize} bytes)."
        });

        await _context.SaveChangesAsync();

        // Clean up old file from storage
        await _fileStorageService.DeleteFileAsync(oldStorageKey);

        return true;
    }

    public async Task<bool> DeleteDocumentAsync(int documentId, int requestingUserId)
    {
        var document = await _context.Documents
            .Include(d => d.Project)
            .Include(d => d.Shares)
            .FirstOrDefaultAsync(d => d.DocumentId == documentId);

        if (document == null) return false;

        var user = await _context.Users.FindAsync(requestingUserId);
        if (user == null) return false;

        var canDelete = user.Role == UserRole.Administrator ||
                        document.UploadedByUserId == requestingUserId ||
                        (document.ProjectId.HasValue && document.Project != null && document.Project.ProjectManagerId == requestingUserId);

        if (!canDelete) return false;

        // Audit deletion before removal
        _context.DocumentAuditLogs.Add(new DocumentAuditLog
        {
            DocumentId = null, // Detach document reference so audit survives
            UserId = requestingUserId,
            ActionType = "Delete",
            Timestamp = DateTime.UtcNow,
            Details = $"Deleted document '{document.Title}' (File: '{document.OriginalFileName}', Size: {document.FileSize} bytes)."
        });

        // Detach task reference if attached (cascade detachment per FR-016)
        document.TaskId = null;

        // Delete physical file
        await _fileStorageService.DeleteFileAsync(document.StorageKey);

        // Delete database record
        _context.Documents.Remove(document);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> ShareDocumentWithUserAsync(int documentId, int targetUserId, int requestingUserId)
    {
        var isAuthorized = await AuthorizeAccessAsync(documentId, requestingUserId);
        if (!isAuthorized) return false;

        var document = await _context.Documents.FindAsync(documentId);
        if (document == null) return false;

        var targetUser = await _context.Users.FindAsync(targetUserId);
        if (targetUser == null) return false;

        var existingShare = await _context.DocumentShares
            .FirstOrDefaultAsync(s => s.DocumentId == documentId && s.SharedWithUserId == targetUserId);

        if (existingShare != null) return true; // Already shared

        var share = new DocumentShare
        {
            DocumentId = documentId,
            SharedWithUserId = targetUserId,
            SharedByUserId = requestingUserId,
            SharedDate = DateTime.UtcNow,
            Permission = "ReadOnly"
        };

        _context.DocumentShares.Add(share);

        _context.DocumentAuditLogs.Add(new DocumentAuditLog
        {
            DocumentId = documentId,
            UserId = requestingUserId,
            ActionType = "Share",
            Timestamp = DateTime.UtcNow,
            Details = $"Shared document with user '{targetUser.DisplayName}'."
        });

        await _context.SaveChangesAsync();

        var sharingUser = await _context.Users.FindAsync(requestingUserId);
        await _notificationService.CreateNotificationAsync(new Notification
        {
            UserId = targetUserId,
            Title = "Document Shared With You",
            Message = $"{sharingUser?.DisplayName ?? "A colleague"} shared the document '{document.Title}' with you.",
            Type = NotificationType.SystemAlert,
            Priority = NotificationPriority.Normal
        });

        return true;
    }

    public async Task<bool> ShareDocumentWithDepartmentAsync(int documentId, string department, int requestingUserId)
    {
        var isAuthorized = await AuthorizeAccessAsync(documentId, requestingUserId);
        if (!isAuthorized) return false;

        var document = await _context.Documents.FindAsync(documentId);
        if (document == null) return false;

        var existingShare = await _context.DocumentShares
            .FirstOrDefaultAsync(s => s.DocumentId == documentId && s.SharedWithDepartment == department);

        if (existingShare != null) return true;

        var share = new DocumentShare
        {
            DocumentId = documentId,
            SharedWithDepartment = department,
            SharedByUserId = requestingUserId,
            SharedDate = DateTime.UtcNow,
            Permission = "ReadOnly"
        };

        _context.DocumentShares.Add(share);

        _context.DocumentAuditLogs.Add(new DocumentAuditLog
        {
            DocumentId = documentId,
            UserId = requestingUserId,
            ActionType = "Share",
            Timestamp = DateTime.UtcNow,
            Details = $"Shared document with department '{department}'."
        });

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<Document>> GetTaskDocumentsAsync(int taskId, int requestingUserId)
    {
        var task = await _context.Tasks
            .Include(t => t.Project)
                .ThenInclude(p => p!.ProjectMembers)
            .FirstOrDefaultAsync(t => t.TaskId == taskId);

        if (task == null) return new List<Document>();

        return await _context.Documents
            .Include(d => d.UploadedByUser)
            .Where(d => d.TaskId == taskId)
            .OrderByDescending(d => d.CreatedDate)
            .ToListAsync();
    }

    public async Task<bool> AttachDocumentToTaskAsync(int documentId, int taskId, int requestingUserId)
    {
        var document = await _context.Documents.FindAsync(documentId);
        if (document == null) return false;

        var task = await _context.Tasks.FindAsync(taskId);
        if (task == null) return false;

        document.TaskId = taskId;
        if (task.ProjectId.HasValue && !document.ProjectId.HasValue)
        {
            document.ProjectId = task.ProjectId.Value;
        }

        _context.DocumentAuditLogs.Add(new DocumentAuditLog
        {
            DocumentId = documentId,
            UserId = requestingUserId,
            ActionType = "AttachToTask",
            Timestamp = DateTime.UtcNow,
            Details = $"Attached document to task #{taskId} ('{task.Title}')."
        });

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DetachDocumentFromTaskAsync(int documentId, int taskId, int requestingUserId)
    {
        var document = await _context.Documents.FindAsync(documentId);
        if (document == null || document.TaskId != taskId) return false;

        document.TaskId = null;

        _context.DocumentAuditLogs.Add(new DocumentAuditLog
        {
            DocumentId = documentId,
            UserId = requestingUserId,
            ActionType = "DetachFromTask",
            Timestamp = DateTime.UtcNow,
            Details = $"Detached document from task #{taskId}."
        });

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<DocumentAuditLog>> GetDocumentAuditLogsAsync(int? documentId, int requestingUserId)
    {
        var user = await _context.Users.FindAsync(requestingUserId);
        if (user == null) return new List<DocumentAuditLog>();

        var query = _context.DocumentAuditLogs
            .Include(a => a.User)
            .Include(a => a.Document)
            .AsQueryable();

        if (user.Role != UserRole.Administrator)
        {
            // Non-admin can only see logs for documents they own or actions they performed
            query = query.Where(a => a.UserId == requestingUserId || (a.Document != null && a.Document.UploadedByUserId == requestingUserId));
        }

        if (documentId.HasValue)
        {
            query = query.Where(a => a.DocumentId == documentId.Value);
        }

        return await query.OrderByDescending(a => a.Timestamp).Take(200).ToListAsync();
    }

    public async Task<DocumentSummaryStats> GetDocumentStatsAsync(int requestingUserId)
    {
        var user = await _context.Users.FindAsync(requestingUserId);
        var userDept = user?.Department ?? string.Empty;
        var isAdmin = user?.Role == UserRole.Administrator;

        var allQuery = _context.Documents.AsQueryable();
        if (!isAdmin)
        {
            allQuery = allQuery.Where(d =>
                d.UploadedByUserId == requestingUserId ||
                (d.ProjectId.HasValue && (d.Project!.ProjectManagerId == requestingUserId || d.Project.ProjectMembers.Any(pm => pm.UserId == requestingUserId))) ||
                d.Shares.Any(s => s.SharedWithUserId == requestingUserId || (!string.IsNullOrEmpty(s.SharedWithDepartment) && s.SharedWithDepartment == userDept))
            );
        }

        var total = await allQuery.CountAsync();
        var personal = await _context.Documents.CountAsync(d => d.UploadedByUserId == requestingUserId);
        var project = await _context.Documents.CountAsync(d => d.ProjectId.HasValue && d.UploadedByUserId == requestingUserId);
        var shared = await _context.Documents.CountAsync(d => d.UploadedByUserId != requestingUserId &&
            d.Shares.Any(s => s.SharedWithUserId == requestingUserId || (!string.IsNullOrEmpty(s.SharedWithDepartment) && s.SharedWithDepartment == userDept)));

        return new DocumentSummaryStats
        {
            TotalAccessibleDocuments = total,
            PersonalDocumentsCount = personal,
            ProjectDocumentsCount = project,
            SharedWithMeCount = shared
        };
    }
}
