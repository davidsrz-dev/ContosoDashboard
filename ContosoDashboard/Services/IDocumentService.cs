using ContosoDashboard.Models;

namespace ContosoDashboard.Services;

public interface IDocumentService
{
    // Retrieval Operations with Service-Level Authorization (IDOR Prevention)
    Task<List<Document>> GetUserDocumentsAsync(int requestingUserId);
    Task<List<Document>> GetProjectDocumentsAsync(int projectId, int requestingUserId);
    Task<List<Document>> GetSharedDocumentsAsync(int requestingUserId);
    Task<List<Document>> SearchDocumentsAsync(string searchTerm, string? category, int? projectId, int requestingUserId);
    Task<Document?> GetDocumentByIdAsync(int documentId, int requestingUserId);

    // Mutation Operations
    Task<Document> UploadDocumentAsync(DocumentUploadModel model, int requestingUserId);
    Task<bool> UpdateDocumentMetadataAsync(int documentId, DocumentEditModel model, int requestingUserId);
    Task<bool> ReplaceDocumentFileAsync(int documentId, Stream newFileStream, string newFileName, string contentType, long fileSize, int requestingUserId);
    Task<bool> DeleteDocumentAsync(int documentId, int requestingUserId);

    // Sharing & Task Workflows
    Task<bool> ShareDocumentWithUserAsync(int documentId, int targetUserId, int requestingUserId);
    Task<bool> ShareDocumentWithDepartmentAsync(int documentId, string department, int requestingUserId);
    Task<List<Document>> GetTaskDocumentsAsync(int taskId, int requestingUserId);
    Task<bool> AttachDocumentToTaskAsync(int documentId, int taskId, int requestingUserId);
    Task<bool> DetachDocumentFromTaskAsync(int documentId, int taskId, int requestingUserId);

    // Authorization & Audit
    Task<bool> AuthorizeAccessAsync(int documentId, int requestingUserId);
    Task<List<DocumentAuditLog>> GetDocumentAuditLogsAsync(int? documentId, int requestingUserId);
    Task<DocumentSummaryStats> GetDocumentStatsAsync(int requestingUserId);
}
