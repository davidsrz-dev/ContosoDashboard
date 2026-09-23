# Research & Technical Decisions: Document Upload and Management

**Feature**: `001-document-management`  
**Date**: 2026-09-23  
**Status**: Completed  

---

## 1. Storage Architecture & Cloud Migration Path

### Decision
Implement an abstracted storage interface `IFileStorageService` backed by `LocalFileStorageService` for local development and offline training, storing physical files in `AppData/uploads/` outside the web root (`wwwroot`).

### Rationale
- Complies strictly with Constitution Principle I (*Offline-First*) and Principle II (*Infrastructure Abstraction*).
- Prevents public URL access to uploaded files, enabling service-level authorization (IDOR prevention) before streaming file contents to the client.
- Provides a clean swap path to `AzureBlobStorageService` via Dependency Injection in `Program.cs` without altering any business logic or UI code.

### Alternatives Considered
- *Storing directly in `wwwroot/uploads`*: Rejected because it bypasses application authorization, allowing anyone with the URL to view documents (severe IDOR violation).
- *Storing files as `VARBINARY(MAX)` / BLOBs directly in SQL Server*: Rejected because database bloat degrades performance, backup sizes balloon, and migration to Azure Blob Storage becomes complex.

---

## 2. File Path Generation & Storage Safety Sequencing

### Decision
Generate unique storage keys using a structured GUID pattern before database insertion:
`{userId}/{projectId or "personal"}/{guid}.{extension}`

**Sequencing**:
1. Validate MIME type, extension whitelist, and file size (< 25 MB).
2. Generate unique storage key and physical directory path.
3. Write file stream to physical disk.
4. Save metadata record (`Document`) to Entity Framework Core database context.
5. If database save fails, purge physical file immediately to prevent orphaned files.

### Rationale
- Enforces Constitution Principle IV (*Storage Safety & Atomic Data Operations*).
- Prevents path traversal vulnerabilities (`../../`) by never using user-supplied filenames for filesystem paths.
- Avoids duplicate key collisions when multiple users upload files with identical names (e.g., `Report.docx`).
- Guarantees zero orphaned records in the database and zero orphaned files on disk.

### Alternatives Considered
- *Database record first, then file write*: Rejected because database rollback with file write errors is harder to coordinate and risks holding open database locks during slow file I/O.
- *Using original filename on disk*: Rejected because of security vulnerabilities (directory traversal, command injection) and name collision risks.

---

## 3. In-Browser Document Preview & Streaming Endpoint

### Decision
Implement an authorized ASP.NET Core Controller endpoint (`GET /api/documents/{id}/stream` and `GET /api/documents/{id}/download`) that:
1. Validates user authorization via `IDocumentService.AuthorizeAccessAsync(documentId, requestingUserId)`.
2. Serves PDFs (`application/pdf`) and images (`image/jpeg`, `image/png`) with `Content-Disposition: inline` for in-browser rendering.
3. Serves other files or download requests with `Content-Disposition: attachment; filename="{OriginalFileName}"`.
4. Renders PDF/image previews in a reusable Blazor modal/component using an `<iframe>` or `<img>` element pointing to the authorized stream endpoint.

### Rationale
- Blazor Server WebSocket connections cannot efficiently transfer large binary streams for direct browser rendering without high latency or signal saturation.
- Native HTTP streaming endpoints leverage browser caching, range requests, and built-in PDF/image renderers while enforcing server-side authorization.

### Alternatives Considered
- *Base64 encoding streams over Blazor Server SignalR circuit*: Rejected due to ~33% memory overhead and potential circuit disconnection on large files.
- *Direct file links*: Rejected as files are outside `wwwroot` for IDOR security.

---

## 4. Authorization & IDOR Protection Model

### Decision
Enforce authorization inside `DocumentService` for all CRUD, download, share, and delete actions:
- **Employee**: Can view/download documents they own, documents shared with them directly or via department, and documents in projects where they are an active member.
- **Team Lead**: Inherits Employee access plus can view/download documents uploaded by members of their department/team.
- **Project Manager**: Can view, upload, download, and delete any document associated with projects they manage.
- **Administrator**: Full audit inspection access across all documents and audit events.

### Rationale
- Complies strictly with Constitution Principle III (*Defense-in-Depth & IDOR Prevention*).
- Prevents unauthorized access if users manipulate IDs in URLs, Blazor parameters, or API calls.

---

## 5. Audit Logging & Notification Integration

### Decision
Create a lightweight entity `DocumentAuditLog` and utilize the existing `INotificationService` in `ContosoDashboard.Services` to trigger alerts for document sharing and project uploads.

### Rationale
- Maintains complete traceability for compliance and audit reporting without requiring external SIEM dependencies.
- Reuses existing notification UI badges and toast systems already present in the ContosoDashboard codebase.
