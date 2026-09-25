# Data Model: Document Upload and Management

**Feature**: `001-document-management`
**Date**: 2026-09-24
**Status**: Existing schema reviewed for gap closure

---

## Entity Relationships

```mermaid
erDiagram
    User ||--o{ Document : "uploads"
    Project ||--o{ Document : "contains"
    TaskItem ||--o{ Document : "references"
    Document ||--o{ DocumentShare : "shared via"
    Document ||--o{ DocumentAuditLog : "generates"
    User ||--o{ DocumentShare : "shared with"
    User ||--o{ DocumentAuditLog : "performed by"

    Document {
        int DocumentId PK
        string Title
        string Description
        string Category
        string OriginalFileName
        string StorageKey
        long FileSize
        string ContentType
        datetime CreatedDate
        datetime UpdatedDate
        int UploadedByUserId FK
        int ProjectId FK
        int TaskId FK
        string Tags
    }

    DocumentShare {
        int DocumentShareId PK
        int DocumentId FK
        int SharedWithUserId FK
        string SharedWithDepartment
        int SharedByUserId FK
        datetime SharedDate
        string Permission
    }

    DocumentAuditLog {
        int DocumentAuditLogId PK
        int DocumentId FK
        string ActionType
        int UserId FK
        datetime Timestamp
        string Details
    }
```

---

## Entities Specification

### 1. `Document`

Represents an uploaded document with associated metadata and storage pointers.

| Column | Type | Nullable | Description / Validation |
|---|---|---|---|
| `DocumentId` | `int` | No (PK) | Auto-incrementing primary key. |
| `Title` | `nvarchar(200)` | No | Display title provided by user. Max 200 chars. |
| `Description` | `nvarchar(1000)` | Yes | Optional descriptive notes. Max 1000 chars. |
| `Category` | `nvarchar(50)` | No | Predefined values: *Project Documents*, *Team Resources*, *Personal Files*, *Reports*, *Presentations*, *Other*. |
| `OriginalFileName` | `nvarchar(260)` | No | Original client filename for user downloads. |
| `StorageKey` | `nvarchar(500)` | No | Unique sanitized relative storage path (e.g., `1/personal/a1b2c3d4-....pdf`). |
| `FileSize` | `bigint` | No | Size in bytes (max 26,214,400 bytes = 25 MB). |
| `ContentType` | `nvarchar(255)` | No | MIME type (e.g., `application/pdf`, `image/png`). Whitelisted types only. |
| `CreatedDate` | `datetime2` | No | Timestamp of upload (UTC); used for inclusive UTC calendar-date filtering and report time buckets. |
| `UpdatedDate` | `datetime2` | No | Timestamp of last metadata edit or file replacement (UTC). |
| `UploadedByUserId` | `int` | No (FK) | Reference to `User.UserId`. |
| `ProjectId` | `int` | Yes (FK) | Optional reference to `Project.ProjectId`. |
| `TaskId` | `int` | Yes (FK) | Optional reference to `TaskItem.TaskId`. Set to `null` if task reference is detached. |
| `Tags` | `nvarchar(500)` | Yes | Comma-separated search tags (e.g., "q3, financial, budget"). |

**Navigation Properties**:
- `UploadedByUser` (`User`)
- `Project` (`Project?`)
- `Task` (`TaskItem?`)
- `Shares` (`ICollection<DocumentShare>`)
- `AuditLogs` (`ICollection<DocumentAuditLog>`)

---

### 2. `DocumentShare`

Tracks direct user or department sharing permissions.

| Column | Type | Nullable | Description / Validation |
|---|---|---|---|
| `DocumentShareId` | `int` | No (PK) | Auto-incrementing primary key. |
| `DocumentId` | `int` | No (FK) | Reference to `Document.DocumentId` (Cascade delete on document deletion). |
| `SharedWithUserId` | `int` | Yes (FK) | Specific recipient user. Null if sharing by department. |
| `SharedWithDepartment` | `nvarchar(100)` | Yes | Target department name. Null if sharing by specific user. |
| `SharedByUserId` | `int` | No (FK) | Reference to grantor `User.UserId`. |
| `SharedDate` | `datetime2` | No | Timestamp of share action (UTC). |
| `Permission` | `nvarchar(20)` | No | Default: `"ReadOnly"`. |

---

### 3. `DocumentAuditLog`

Immutable record of document lifecycle operations.

| Column | Type | Nullable | Description / Validation |
|---|---|---|---|
| `DocumentAuditLogId` | `int` | No (PK) | Auto-incrementing primary key. |
| `DocumentId` | `int` | Yes | Reference to document (retained even if document is later deleted). |
| `ActionType` | `nvarchar(50)` | No | Actions include `Upload`, `Download`, `Preview`, `EditMetadata`, `ReplaceFile`, `Share`, `Delete`, `AttachToTask`, and `DetachFromTask`. |
| `UserId` | `int` | No (FK) | User who executed the action. |
| `Timestamp` | `datetime2` | No | Timestamp of occurrence (UTC). |
| `Details` | `nvarchar(1000)` | Yes | Details such as filename, target recipient, or updated fields. |

---

## State Transitions & Lifecycle Rules

1. **Upload**: User selects file → Validation passes → File written to storage → Record created with `CreatedDate = UtcNow`.
2. **Edit Metadata**: User updates Title/Description/Category/Tags → Record updated with `UpdatedDate = UtcNow` → Audit log entry `EditMetadata` created.
3. **Replace File**: User uploads new file for existing document → Validation passes → New file saved to storage → Old file purged → FileSize, ContentType, and `UpdatedDate` updated → Audit log entry `ReplaceFile` created.
4. **Task Detachment**: Associated task is deleted or document is detached → `TaskId` set to `null` → Document remains accessible in project/personal views.
5. **Deletion**:
   - Check if attached to active tasks → Display confirmation prompt with task list.
   - Upon confirmation: Remove `TaskId` reference, remove physical file via `IFileStorageService.DeleteFileAsync`, delete `DocumentShare` records, remove `Document` record, and write a `Delete` entry to `DocumentAuditLog` with a null `DocumentId` so the audit record is retained.

## Gap-Closure Data Requirements

- **No new persistent entity is required** for multi-file upload: the batch exists only in the request/UI state, with one `Document` record and one atomic storage operation per file. The UI receives an independent success/error result per selected file.
- **No new persistent entity is required** for date filtering: query `Document.CreatedDate` as UTC. The start and end inputs are inclusive UTC calendar dates; implement the end bound as the start of the following day, exclusive.
- **Task uploads** reuse `Document.TaskId` and `Document.ProjectId`. Both IDs must be resolved from the server-loaded task; the browser must not choose an unrelated parent project.
- **Access audit events** reuse `DocumentAuditLog`. Record `Download` for `/download` and `Preview` for `/stream` after authorization and file lookup. Keep deletion audit records even after the document row is removed, as the current model permits a nullable document reference.
- **Administrator summary reports** are read-only aggregates over `Document` and `DocumentAuditLog`: document type counts, upload counts by user, and access counts by action/time range. They do not require stored report snapshots.
- **Adoption measurement** reuses the existing `User.LastLoginDate` and `Document.CreatedDate` values. Count distinct users whose last login and successful upload occur within the same three-month deployment window.
- **Scope exclusions**: no scan-result field, malware service, cloud storage metadata, or Azure-specific entity is introduced.

## Transient Service DTOs

These are request/response models only and are not persisted:

- **`DocumentUploadResult`**: original filename, success flag, optional `DocumentId`, and user-readable error. There is one result for every file submitted, in submission order.
- **`DocumentActivityReport`**: selected UTC date range plus collections for document counts grouped by MIME/extension, upload counts grouped by user, and Preview/Download counts grouped by action and time bucket. Only the Administrator service path can request it.
