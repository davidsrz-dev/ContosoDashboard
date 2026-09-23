# Service Interface Contracts: Document Management

**Feature**: `001-document-management`  
**Date**: 2026-09-23  

---

## 1. Storage Abstraction Contract (`IFileStorageService`)

Location: `ContosoDashboard.Services.IFileStorageService`

```csharp
namespace ContosoDashboard.Services;

public interface IFileStorageService
{
    /// <summary>
    /// Saves a file stream to the underlying storage provider.
    /// </summary>
    /// <param name="fileStream">The readable stream containing file data.</param>
    /// <param name="relativeStorageKey">The target storage key / relative path.</param>
    /// <returns>The confirmed relative storage key.</returns>
    Task<string> SaveFileAsync(Stream fileStream, string relativeStorageKey);

    /// <summary>
    /// Retrieves a file stream from the underlying storage provider.
    /// </summary>
    /// <param name="relativeStorageKey">The relative storage key.</param>
    /// <returns>A readable Stream or null if the file does not exist.</returns>
    Task<Stream?> GetFileStreamAsync(string relativeStorageKey);

    /// <summary>
    /// Deletes a file from the underlying storage provider.
    /// </summary>
    /// <param name="relativeStorageKey">The relative storage key.</param>
    /// <returns>True if deletion succeeded, false otherwise.</returns>
    Task<bool> DeleteFileAsync(string relativeStorageKey);

    /// <summary>
    /// Verifies whether a file exists in the storage provider.
    /// </summary>
    Task<bool> FileExistsAsync(string relativeStorageKey);
}
```

---

## 2. Business Service Contract (`IDocumentService`)

Location: `ContosoDashboard.Services.IDocumentService`

```csharp
namespace ContosoDashboard.Services;

public interface IDocumentService
{
    // Retrieval Operations (with IDOR Authorization Enforcement)
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

    // Sharing & Tasks
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
```
