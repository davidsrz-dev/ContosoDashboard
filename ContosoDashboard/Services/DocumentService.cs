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

    private static readonly Dictionary<string, string[]> AllowedExtensionToMimes = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".pdf", new[] { "application/pdf" } },
        { ".doc", new[] { "application/msword" } },
        { ".docx", new[] { "application/vnd.openxmlformats-officedocument.wordprocessingml.document" } },
        { ".xls", new[] { "application/vnd.ms-excel" } },
        { ".xlsx", new[] { "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" } },
        { ".ppt", new[] { "application/vnd.ms-powerpoint" } },
        { ".pptx", new[] { "application/vnd.openxmlformats-officedocument.presentationml.presentation" } },
        { ".txt", new[] { "text/plain" } },
        { ".jpg", new[] { "image/jpeg", "image/pjpeg" } },
        { ".jpeg", new[] { "image/jpeg", "image/pjpeg" } },
        { ".png", new[] { "image/png" } }
    };

    private const long MaxFileSizeInBytes = 26214400; // 25 MB

    public static (bool IsValid, string? ErrorMessage) ValidateFile(string fileName, string contentType, long fileSize)
    {
        if (fileSize <= 0)
            return (false, "El archivo no puede estar vacío.");

        if (fileSize > MaxFileSizeInBytes)
            return (false, $"El archivo excede el tamaño máximo permitido de 25 MB ({MaxFileSizeInBytes} bytes).");

        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensionToMimes.TryGetValue(extension, out var validMimes))
            return (false, $"El tipo de archivo '{extension}' no está permitido. Extensiones soportadas: {string.Join(", ", AllowedExtensionToMimes.Keys)}.");

        if (string.IsNullOrWhiteSpace(contentType))
            return (false, "El tipo de contenido (MIME) es obligatorio.");

        if (contentType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase))
            return (false, "El tipo MIME genérico 'application/octet-stream' no está permitido. Debe especificar el tipo MIME exacto.");

        if (!validMimes.Any(m => m.Equals(contentType, StringComparison.OrdinalIgnoreCase)))
        {
            return (false, $"El tipo MIME '{contentType}' no coincide con la extensión '{extension}'.");
        }

        return (true, null);
    }

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

    public async Task<List<Document>> GetDepartmentDocumentsAsync(int requestingUserId)
    {
        var user = await _context.Users.FindAsync(requestingUserId);
        if (user == null || string.IsNullOrEmpty(user.Department)) return new List<Document>();

        // Accessible if TeamLead, Administrator, or member of that department
        var isTeamLeadOrAdmin = user.Role == UserRole.TeamLead || user.Role == UserRole.Administrator;
        if (!isTeamLeadOrAdmin)
        {
            return new List<Document>();
        }

        return await _context.Documents
            .Include(d => d.UploadedByUser)
            .Include(d => d.Project)
            .Include(d => d.Task)
            .Where(d => d.UploadedByUser != null && d.UploadedByUser.Department == user.Department)
            .OrderByDescending(d => d.CreatedDate)
            .ToListAsync();
    }

    public Task<List<Document>> SearchDocumentsAsync(string searchTerm, string? category, int? projectId, int requestingUserId)
    {
        return SearchDocumentsAsync(searchTerm, category, projectId, null, null, requestingUserId);
    }

    public async Task<List<Document>> SearchDocumentsAsync(
        string searchTerm,
        string? category,
        int? projectId,
        DateOnly? startDateUtc,
        DateOnly? endDateUtc,
        int requestingUserId)
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
                (user.Role == UserRole.TeamLead && d.UploadedByUser != null && d.UploadedByUser.Department == userDept)
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

        if (startDateUtc.HasValue)
        {
            var startDateTime = startDateUtc.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(d => d.CreatedDate >= startDateTime);
        }

        if (endDateUtc.HasValue)
        {
            var endExclusive = endDateUtc.Value.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(d => d.CreatedDate < endExclusive);
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
                (d.UploadedByUser != null && d.UploadedByUser.DisplayName.Contains(term))
            );
        }

        return await query.OrderByDescending(d => d.CreatedDate).ToListAsync();
    }

    public async Task<bool> CheckDuplicateTitleAsync(string title, string category, int? projectId)
    {
        if (string.IsNullOrWhiteSpace(title)) return false;

        var cleanTitle = title.Trim().ToLower();
        var query = _context.Documents.Where(d => d.Title.ToLower() == cleanTitle && d.Category == category);

        if (projectId.HasValue)
        {
            query = query.Where(d => d.ProjectId == projectId.Value);
        }

        return await query.AnyAsync();
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
            throw new ArgumentException("El contenido del archivo no puede estar vacío.");

        var validation = ValidateFile(model.FileName, model.ContentType, model.FileSize);
        if (!validation.IsValid)
            throw new InvalidOperationException(validation.ErrorMessage);

        var extension = Path.GetExtension(model.FileName);

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
                    Type = NotificationType.ProjectUpdate,
                    Priority = NotificationPriority.Informational
                });
            }
        }

        return document;
    }

    public async Task<IReadOnlyList<DocumentUploadResult>> UploadDocumentsAsync(
        IReadOnlyList<DocumentUploadModel> files,
        int requestingUserId)
    {
        var results = new List<DocumentUploadResult>();

        foreach (var fileModel in files)
        {
            var fileName = string.IsNullOrWhiteSpace(fileModel.FileName) ? "document" : Path.GetFileName(fileModel.FileName);
            var validation = ValidateFile(fileName, fileModel.ContentType, fileModel.FileSize);
            if (!validation.IsValid)
            {
                results.Add(new DocumentUploadResult
                {
                    OriginalFileName = fileName,
                    Success = false,
                    ErrorMessage = validation.ErrorMessage,
                    FileSize = fileModel.FileSize
                });
                continue;
            }

            try
            {
                var doc = await UploadDocumentAsync(fileModel, requestingUserId);
                results.Add(new DocumentUploadResult
                {
                    OriginalFileName = doc.OriginalFileName,
                    Success = true,
                    DocumentId = doc.DocumentId,
                    FileSize = doc.FileSize
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload file {FileName} for user {UserId}", fileName, requestingUserId);
                results.Add(new DocumentUploadResult
                {
                    OriginalFileName = fileName,
                    Success = false,
                    ErrorMessage = ex.Message,
                    FileSize = fileModel.FileSize
                });
            }
        }

        return results;
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

        // Verify destination project access if ProjectId is changed or assigned
        if (model.ProjectId.HasValue && model.ProjectId != document.ProjectId)
        {
            var targetProject = await _context.Projects
                .Include(p => p.ProjectMembers)
                .FirstOrDefaultAsync(p => p.ProjectId == model.ProjectId.Value);

            if (targetProject == null)
            {
                return false;
            }

            var hasTargetProjectAccess = user.Role == UserRole.Administrator ||
                                         targetProject.ProjectManagerId == requestingUserId ||
                                         targetProject.ProjectMembers.Any(pm => pm.UserId == requestingUserId);

            if (!hasTargetProjectAccess)
            {
                _logger.LogWarning("User {UserId} attempted to move document {DocumentId} to unauthorized project {ProjectId}", requestingUserId, documentId, model.ProjectId.Value);
                return false;
            }
        }

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

        var validation = ValidateFile(newFileName, contentType, fileSize);
        if (!validation.IsValid)
            throw new InvalidOperationException(validation.ErrorMessage);

        var extension = Path.GetExtension(newFileName);

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

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist database updates during document file replacement for DocId {DocumentId}. Purging new physical file {NewStorageKey}.", documentId, newStorageKey);
            await _fileStorageService.DeleteFileAsync(newStorageKey);
            throw;
        }

        // Clean up old file from storage only after DB save succeeds
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

        // Delete physical file and check outcome
        var physicalDeleted = await _fileStorageService.DeleteFileAsync(document.StorageKey);
        if (!physicalDeleted)
        {
            _logger.LogWarning("Physical file '{StorageKey}' was not found or could not be deleted while removing document {DocumentId}.", document.StorageKey, documentId);
        }

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
            Type = NotificationType.SystemAnnouncement,
            Priority = NotificationPriority.Informational
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
            .Include(t => t.AssignedUser)
            .FirstOrDefaultAsync(t => t.TaskId == taskId);

        if (task == null) return new List<Document>();

        var user = await _context.Users.FindAsync(requestingUserId);
        if (user == null) return new List<Document>();

        var canAccessTask = user.Role == UserRole.Administrator ||
                            task.AssignedUserId == requestingUserId ||
                            task.CreatedByUserId == requestingUserId ||
                            (task.Project != null && (task.Project.ProjectManagerId == requestingUserId ||
                                                      task.Project.ProjectMembers.Any(pm => pm.UserId == requestingUserId))) ||
                            (user.Role == UserRole.TeamLead && task.AssignedUser != null &&
                             !string.IsNullOrEmpty(task.AssignedUser.Department) &&
                             task.AssignedUser.Department.Equals(user.Department, StringComparison.OrdinalIgnoreCase));

        if (!canAccessTask)
        {
            _logger.LogWarning("Unauthorized task document access attempt: User {UserId} to Task {TaskId}", requestingUserId, taskId);
            return new List<Document>();
        }

        var docs = await _context.Documents
            .Include(d => d.UploadedByUser)
            .Include(d => d.Project)
            .Where(d => d.TaskId == taskId)
            .OrderByDescending(d => d.CreatedDate)
            .ToListAsync();

        var authorizedDocs = new List<Document>();
        foreach (var doc in docs)
        {
            if (await AuthorizeAccessAsync(doc.DocumentId, requestingUserId))
            {
                authorizedDocs.Add(doc);
            }
        }

        return authorizedDocs;
    }

    public async Task<bool> AttachDocumentToTaskAsync(int documentId, int taskId, int requestingUserId)
    {
        var document = await _context.Documents.FindAsync(documentId);
        if (document == null) return false;

        var task = await _context.Tasks
            .Include(t => t.Project)
                .ThenInclude(p => p!.ProjectMembers)
            .FirstOrDefaultAsync(t => t.TaskId == taskId);
        if (task == null) return false;

        var user = await _context.Users.FindAsync(requestingUserId);
        if (user == null) return false;

        // Verify user has task access
        var canAccessTask = user.Role == UserRole.Administrator ||
                            task.AssignedUserId == requestingUserId ||
                            task.CreatedByUserId == requestingUserId ||
                            (task.Project != null && (task.Project.ProjectManagerId == requestingUserId ||
                                                      task.Project.ProjectMembers.Any(pm => pm.UserId == requestingUserId)));

        if (!canAccessTask)
        {
            _logger.LogWarning("User {UserId} cannot attach document {DocumentId} to task {TaskId}: Task access denied.", requestingUserId, documentId, taskId);
            return false;
        }

        // Verify user has document access
        var canAccessDoc = await AuthorizeAccessAsync(documentId, requestingUserId);
        if (!canAccessDoc)
        {
            _logger.LogWarning("User {UserId} cannot attach document {DocumentId} to task {TaskId}: Document access denied.", requestingUserId, documentId, taskId);
            return false;
        }

        // Prevent attaching across mismatched projects unless admin or project manager
        if (task.ProjectId.HasValue && document.ProjectId.HasValue && task.ProjectId.Value != document.ProjectId.Value)
        {
            var isPrivileged = user.Role == UserRole.Administrator ||
                               (task.Project != null && task.Project.ProjectManagerId == requestingUserId);
            if (!isPrivileged)
            {
                _logger.LogWarning("User {UserId} attempted cross-project attachment of document {DocumentId} to task {TaskId}", requestingUserId, documentId, taskId);
                return false;
            }
        }

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

        var task = await _context.Tasks
            .Include(t => t.Project)
            .FirstOrDefaultAsync(t => t.TaskId == taskId);
        if (task == null) return false;

        var user = await _context.Users.FindAsync(requestingUserId);
        if (user == null) return false;

        // Only Admin, document owner, task PM, or task assignee can detach
        var canDetach = user.Role == UserRole.Administrator ||
                        document.UploadedByUserId == requestingUserId ||
                        task.AssignedUserId == requestingUserId ||
                        (task.Project != null && task.Project.ProjectManagerId == requestingUserId);

        if (!canDetach)
        {
            _logger.LogWarning("User {UserId} unauthorized to detach document {DocumentId} from task {TaskId}", requestingUserId, documentId, taskId);
            return false;
        }

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
        var isTeamLead = user?.Role == UserRole.TeamLead;

        var allQuery = _context.Documents
            .Include(d => d.UploadedByUser)
            .Include(d => d.Project)
                .ThenInclude(p => p!.ProjectMembers)
            .Include(d => d.Shares)
            .AsQueryable();

        if (!isAdmin)
        {
            allQuery = allQuery.Where(d =>
                d.UploadedByUserId == requestingUserId ||
                (d.ProjectId.HasValue && (d.Project!.ProjectManagerId == requestingUserId || d.Project.ProjectMembers.Any(pm => pm.UserId == requestingUserId))) ||
                d.Shares.Any(s => s.SharedWithUserId == requestingUserId || (!string.IsNullOrEmpty(s.SharedWithDepartment) && s.SharedWithDepartment == userDept)) ||
                (isTeamLead && !string.IsNullOrEmpty(userDept) && d.UploadedByUser != null && d.UploadedByUser.Department == userDept)
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

    public async Task<Document> UploadTaskDocumentAsync(int taskId, DocumentUploadModel model, int requestingUserId)
    {
        var task = await _context.Tasks
            .Include(t => t.Project)
                .ThenInclude(p => p!.ProjectMembers)
            .FirstOrDefaultAsync(t => t.TaskId == taskId)
            ?? throw new ArgumentException($"Task with ID {taskId} was not found.");

        var user = await _context.Users.FindAsync(requestingUserId)
                   ?? throw new UnauthorizedAccessException("Requesting user does not exist.");

        // Check if user has access to task/project
        var canAccess = user.Role == UserRole.Administrator ||
                        task.AssignedUserId == requestingUserId ||
                        (task.Project != null && (task.Project.ProjectManagerId == requestingUserId ||
                                                  task.Project.ProjectMembers.Any(pm => pm.UserId == requestingUserId)));

        if (!canAccess)
            throw new UnauthorizedAccessException("You are not authorized to upload documents to this task.");

        // Auto-associate parent project and task
        model.TaskId = taskId;
        if (task.ProjectId.HasValue)
        {
            model.ProjectId = task.ProjectId.Value;
        }

        var document = await UploadDocumentAsync(model, requestingUserId);

        // Record AttachToTask audit entry
        _context.DocumentAuditLogs.Add(new DocumentAuditLog
        {
            DocumentId = document.DocumentId,
            UserId = requestingUserId,
            ActionType = "AttachToTask",
            Timestamp = DateTime.UtcNow,
            Details = $"Attached document '{document.OriginalFileName}' to task #{taskId} ('{task.Title}')."
        });
        await _context.SaveChangesAsync();

        return document;
    }

    public async Task<bool> RecordDocumentAccessAsync(int documentId, int requestingUserId, string actionType)
    {
        if (actionType != "Preview" && actionType != "Download")
            throw new ArgumentException("ActionType must be 'Preview' or 'Download'.", nameof(actionType));

        var isAuthorized = await AuthorizeAccessAsync(documentId, requestingUserId);
        if (!isAuthorized) return false;

        var document = await _context.Documents.FindAsync(documentId);
        if (document == null) return false;

        _context.DocumentAuditLogs.Add(new DocumentAuditLog
        {
            DocumentId = documentId,
            UserId = requestingUserId,
            ActionType = actionType,
            Timestamp = DateTime.UtcNow,
            Details = $"{actionType} of document '{document.OriginalFileName}' ({document.FileSize} bytes)."
        });

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<DocumentActivityReport> GetDocumentActivityReportAsync(
        DateOnly? startDateUtc,
        DateOnly? endDateUtc,
        int requestingUserId)
    {
        var user = await _context.Users.FindAsync(requestingUserId);
        if (user == null || user.Role != UserRole.Administrator)
        {
            throw new UnauthorizedAccessException("Activity reports are restricted to Administrators.");
        }

        DateTime? startDateTime = startDateUtc?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        DateTime? endExclusive = endDateUtc?.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var docQuery = _context.Documents.Include(d => d.UploadedByUser).AsQueryable();
        var auditQuery = _context.DocumentAuditLogs.Include(a => a.User).AsQueryable();

        if (startDateTime.HasValue)
        {
            docQuery = docQuery.Where(d => d.CreatedDate >= startDateTime.Value);
            auditQuery = auditQuery.Where(a => a.Timestamp >= startDateTime.Value);
        }

        if (endExclusive.HasValue)
        {
            docQuery = docQuery.Where(d => d.CreatedDate < endExclusive.Value);
            auditQuery = auditQuery.Where(a => a.Timestamp < endExclusive.Value);
        }

        var docs = await docQuery.ToListAsync();
        var logs = await auditQuery.ToListAsync();

        var totalUploads = docs.Count;
        var totalDownloads = logs.Count(l => l.ActionType == "Download");
        var totalPreviews = logs.Count(l => l.ActionType == "Preview");

        var byCategory = docs.GroupBy(d => d.Category)
            .ToDictionary(g => g.Key, g => g.Count());

        var byContentType = docs.GroupBy(d => d.ContentType)
            .ToDictionary(g => g.Key, g => g.Count());

        var topUploaders = docs.Where(d => d.UploadedByUser != null)
            .GroupBy(d => d.UploadedByUser!)
            .Select(g => new UploaderActivityStat
            {
                UserId = g.Key.UserId,
                DisplayName = g.Key.DisplayName,
                Department = g.Key.Department ?? "N/A",
                UploadCount = g.Count(),
                TotalBytesUploaded = g.Sum(d => d.FileSize)
            })
            .OrderByDescending(u => u.UploadCount)
            .Take(10)
            .ToList();

        var dailyActivity = logs
            .GroupBy(l => l.Timestamp.ToString("yyyy-MM-dd"))
            .Select(g => new ActivityPeriodStat
            {
                PeriodLabel = g.Key,
                UploadCount = g.Count(l => l.ActionType == "Upload"),
                DownloadCount = g.Count(l => l.ActionType == "Download"),
                PreviewCount = g.Count(l => l.ActionType == "Preview")
            })
            .OrderBy(a => a.PeriodLabel)
            .ToList();

        return new DocumentActivityReport
        {
            StartDateUtc = startDateUtc,
            EndDateUtc = endDateUtc,
            TotalUploads = totalUploads,
            TotalDownloads = totalDownloads,
            TotalPreviews = totalPreviews,
            DocumentsByCategory = byCategory,
            DocumentsByContentType = byContentType,
            TopUploaders = topUploaders,
            DailyActivityCounts = dailyActivity
        };
    }
}
