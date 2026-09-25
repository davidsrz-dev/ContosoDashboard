# Service Interface Contracts: Document Management

**Feature**: `001-document-management`
**Date**: 2026-09-24
**Status**: Contracts for gap-closure implementation

These signatures describe required behavior. Reuse the current interfaces where signatures already match; do not add an Azure provider or cloud-specific contract in this scope.

## 1. Local File Storage Contract

Location: `ContosoDashboard.Services.IFileStorageService`

The existing local provider boundary remains:

```csharp
Task<string> SaveFileAsync(Stream fileStream, string relativeStorageKey);
Task<Stream?> GetFileStreamAsync(string relativeStorageKey);
Task<bool> DeleteFileAsync(string relativeStorageKey);
Task<bool> FileExistsAsync(string relativeStorageKey);
```

Requirements: store outside `wwwroot`; accept only server-generated relative keys; enforce path containment; do not expose an unauthenticated public file URL.

## 2. Document Retrieval, Search, and Upload

```csharp
Task<List<Document>> GetUserDocumentsAsync(int requestingUserId);
Task<List<Document>> GetProjectDocumentsAsync(int projectId, int requestingUserId);
Task<List<Document>> GetSharedDocumentsAsync(int requestingUserId);
Task<List<Document>> SearchDocumentsAsync(
    string searchTerm,
    string? category,
    int? projectId,
    DateOnly? startDateUtc,
    DateOnly? endDateUtc,
    int requestingUserId);
Task<Document?> GetDocumentByIdAsync(int documentId, int requestingUserId);
Task<Document> UploadDocumentAsync(DocumentUploadModel model, int requestingUserId);
Task<IReadOnlyList<DocumentUploadResult>> UploadDocumentsAsync(
    IReadOnlyList<DocumentUploadModel> files,
    int requestingUserId);
```

`UploadDocumentsAsync` returns one outcome for each input file. Every file is validated and persisted independently. One failure does not undo successful sibling files; a failed file creates neither metadata nor a retained physical file. Each file is limited to 25 MB.

`SearchDocumentsAsync` treats the supplied dates as inclusive UTC calendar dates. The service translates the end date to an exclusive next-day bound and applies authorization before returning rows.

## 3. Document Mutations and Task Links

```csharp
Task<bool> UpdateDocumentMetadataAsync(int documentId, DocumentEditModel model, int requestingUserId);
Task<bool> ReplaceDocumentFileAsync(int documentId, Stream newFileStream, string newFileName,
    string contentType, long fileSize, int requestingUserId);
Task<bool> DeleteDocumentAsync(int documentId, int requestingUserId);
Task<List<Document>> GetTaskDocumentsAsync(int taskId, int requestingUserId);
Task<Document> UploadTaskDocumentAsync(int taskId, DocumentUploadModel model, int requestingUserId);
Task<bool> AttachDocumentToTaskAsync(int documentId, int taskId, int requestingUserId);
Task<bool> DetachDocumentFromTaskAsync(int documentId, int taskId, int requestingUserId);
```

Task-context upload uses `UploadTaskDocumentAsync`:
- Accepts `taskId`, `model` (`Title` [required], `Category` [required, defaults to "Project Documents"], `FileStream`, `FileName`, `ContentType`, `FileSize`, optional `Description`, `Tags`), and `requestingUserId`.
- Validates user has access to the task and its project.
- Automatically resolves and assigns `ProjectId` from the task's parent project.
- Saves the file atomically, creates the `Document` record, and writes both `Upload` and `AttachToTask` audit entries.
- Existing attach and detach operations remain. Service methods independently authorize access and mutations.

## 4. Access Audit and Administrative Reports

```csharp
Task<bool> RecordDocumentAccessAsync(int documentId, int requestingUserId, string actionType);
Task<List<DocumentAuditLog>> GetDocumentAuditLogsAsync(int? documentId, int requestingUserId);
Task<DocumentActivityReport> GetDocumentActivityReportAsync(
    DateOnly? startDateUtc, DateOnly? endDateUtc, int requestingUserId);
Task<DocumentSummaryStats> GetDocumentStatsAsync(int requestingUserId);
```

- `RecordDocumentAccessAsync` accepts only `Download` or `Preview`; callers invoke it only after authorization and file-stream resolution succeed.
- Audit records include document ID, user ID, UTC timestamp, and action details. Existing lifecycle/task audit operations continue to use `DocumentAuditLog`.
- `GetDocumentActivityReportAsync` is Administrator-only and returns aggregates for document types, top uploaders, and access patterns grouped by action and time period. Non-administrators receive denial, not an empty organization-wide report.
- Date bounds follow the same inclusive UTC calendar-date semantics as search.

## 5. Permission Contract

- Owners may read, download, edit/replace, and delete their documents.
- Team Leads may upload documents and read/download their own, department, and assigned-project documents. Team Lead status alone does not grant mutation rights over another user's document.
- A Project Manager may read, download, edit/replace, and delete documents belonging to a project they manage.
- Administrators have organization-wide document and audit access.
- Shared recipients have read-only access unless another independent role grants additional rights.
- Every operation authorizes using current server-side user, project, department, and task data; never trust IDs or role claims supplied by the browser alone.
