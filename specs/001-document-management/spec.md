# Feature Specification: Document Upload and Management

**Feature Branch**: `001-document-management`
**Created**: 2026-09-23
**Status**: Ready for Task Generation (Gap Closure)
**Input**: User description: "Crear la funcionalidad de carga y gestión de documentos (Document Upload and Management) basada en los requisitos de StakeholderDocs/document-upload-and-management-feature.md"

## Clarifications

### Session 2026-09-23
- Q: How should the system handle deleting a document that is currently attached to an active task? (FR-016) → A: Warn & detach: Display a confirmation warning identifying all attached tasks; upon user confirmation, delete the document from physical storage and database records, and automatically detach the task reference.
- Q: How should the system handle uploading a document whose title or filename matches an existing one in the same category or project? (FR-004) → A: Allow with advisory: Allow duplicate titles (differentiated by unique ID and upload timestamp) while presenting an informational notice offering the user the option to either create a new distinct record or replace the existing document file.
- Q: How should the system evaluate access permissions when a document is shared with an entire department? (FR-017) → A: Dynamic evaluation: The service layer dynamically verifies the requesting user's current department claims at request time, ensuring immediate permission updates whenever user department assignments change.
- Q: When a document is uploaded to a shared project, which members should receive an in-app notification? (FR-018) → A: All active members excluding author: Notify all active project members and project managers, excluding the user who performed the upload, to eliminate redundant self-notifications.
- Q: What should be the retention policy for document audit logs? (FR-023) → A: Indefinite immutable retention: Preserve all document audit entries permanently without automated purging to maintain full historical compliance and administrative oversight.

### Session 2026-09-24 — Gap-closure planning
- Scope decision: Antivirus or malware scanning is excluded from this delivery. The existing extension, MIME-type, size, authorization, and safe-storage controls remain required.
- Scope decision: Implement and validate the local filesystem provider only. No Azure Blob Storage provider or Azure deployment work is included in this delivery.
- Team Lead permissions: Team Leads may upload documents and view/download documents uploaded by members of their department and documents in their assigned projects. The detailed operation rules govern mutations: editing/replacing is limited to the owner, Administrator, or responsible Project Manager; deletion is limited to the owner, Administrator, or responsible Project Manager. Team Lead status alone does not grant mutation rights over another user's document.
- Multi-file upload behavior: Each selected file is processed independently. A failed file reports its own error and leaves no partial record or orphan file; successful files in the same selection remain saved.
- Date-range behavior: Start and end dates are inclusive calendar dates evaluated against the UTC upload timestamp.
- Adoption metric: An active user is a user with `LastLoginDate` during the three-month measurement window. The numerator is the distinct active users with at least one successful document upload during that same window.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Secure Document Upload and Personal File Management (Priority: P1)

As a Contoso employee, I want to upload my work documents, provide metadata (title, category, optional description/tags), and view my uploaded files in a dedicated personal dashboard so that my work files are securely organized in one place and readily accessible for download or browser preview.

**Why this priority**: Uploading, storing, listing, and retrieving personal documents represents the core foundation and minimum viable product (MVP). Without this capability, no secondary features (sharing, project association, audit reporting) can function.

**Independent Test**: Can be fully tested by logging in as an employee, uploading a valid PDF document with required metadata, verifying it appears in "My Documents", previewing it in the browser, and downloading it to verify file integrity.

**Acceptance Scenarios**:

1. **Given** an authenticated user on the Document Management page, **When** they select a valid file (e.g., a 5 MB PDF), enter a title, choose a category ("Personal Files"), and submit, **Then** the file uploads with a visual progress indicator and appears immediately in their "My Documents" list with accurate metadata.
2. **Given** an uploaded document in "My Documents", **When** the user clicks "Preview" on a PDF or image file, **Then** the system renders the document preview in the browser without requiring a file download.
3. **Given** an uploaded document in "My Documents", **When** the user clicks "Download", **Then** the original file is delivered to the user's browser with its original filename and extension.
4. **Given** a user attempting to upload a file exceeding 25 MB or with an unsupported file extension (e.g., `.exe` or `.zip`), **When** they attempt to submit the upload, **Then** the system rejects the file before persistence and displays a clear, explanatory validation error message.
5. **Given** a user selects multiple files including both valid and invalid files, **When** they submit the selection, **Then** each file receives its own progress/result status, valid files are saved, invalid files show a clear error, and a failed file does not roll back successful files or leave an orphan.

---

### User Story 2 - Project Association and Team Document Access (Priority: P2)

As a project team member or project manager, I want to associate uploaded documents with specific projects and view all documents linked to my projects so that all project collaborators have direct access to shared project resources.

**Why this priority**: Contoso employees work primarily in project teams. Enabling project association connects document management to existing operational workflows and prevents isolated data silos.

**Independent Test**: Can be fully tested by uploading a document associated with Project A, logging in as another team member assigned to Project A, and verifying that the document is visible, downloadable, and previewable in the Project Documents section.

**Acceptance Scenarios**:

1. **Given** an employee assigned to Project Alpha, **When** they upload a project plan document and select "Project Alpha" from their active projects list, **Then** the document is saved and linked to Project Alpha.
2. **Given** a team member assigned to Project Alpha navigating to the Project Documents tab, **When** they view the project documents, **Then** they see all documents associated with Project Alpha, including uploader name, upload date, and category.
3. **Given** a user who is NOT a member of Project Alpha, **When** they browse project documents or attempt to directly view Project Alpha files, **Then** the system prevents access and excludes those documents from their view.
4. **Given** a Project Manager managing Project Alpha, **When** viewing the project's documents, **Then** they have administrative privileges to manage or remove any document associated with that project.
5. **Given** a Team Lead and a document uploaded by a member of the same department, **When** the Team Lead opens the document, **Then** they can view and download it but cannot edit, replace, or delete it solely because of their Team Lead role.

---

### User Story 3 - Search, Sorting, Filtering, and Metadata Management (Priority: P3)

As a dashboard user managing multiple documents, I want to quickly search documents by keyword, sort by various criteria, filter by category or date, and update metadata or replace document files so that I can easily locate and maintain accurate records over time.

**Why this priority**: As the volume of documents grows, searchability, sorting, and metadata maintenance are essential for productivity, preventing duplicate uploads and outdated file retention.

**Independent Test**: Can be fully tested by creating multiple documents across diverse categories and projects, executing search queries against titles, tags, and descriptions, applying category filters, and updating a document's title and description.

**Acceptance Scenarios**:

1. **Given** a library of uploaded documents, **When** the user enters a search term matching a document tag or keyword in the description, **Then** the system returns matching accessible documents within 2 seconds.
2. **Given** a user viewing their document list, **When** they filter by category "Reports" and sort by "Upload Date (Descending)", **Then** only report documents are displayed, ordered from newest to oldest.
3. **Given** a document owner viewing their document details, **When** they update the title, description, or tags, **Then** the changes are saved and reflected immediately across all views.
4. **Given** a document owner with an updated version of a file, **When** they select "Replace File" and upload a new valid file, **Then** the existing metadata is preserved, the physical content is updated, and the new file size and upload date are recorded.
5. **Given** documents uploaded before, during, and after a selected UTC calendar-date range, **When** the user applies inclusive start and end dates, **Then** documents within both boundary dates are included and documents outside the range are excluded.

---

### User Story 4 - Document Sharing and Notification Workflows (Priority: P4)

As a document owner, I want to share specific documents with individual colleagues or entire departments and have them notified so that cross-functional collaboration is secure, frictionless, and traceable.

**Why this priority**: Collaboration often extends beyond formal project boundaries. Controlled sharing ensures users can distribute files without bypassing authorization policies.

**Independent Test**: Can be fully tested by sharing a personal document from User 1 to User 2, verifying User 2 receives an in-app notification, and confirming the document appears in User 2's "Shared with Me" view while remaining inaccessible to unauthorized User 3.

**Acceptance Scenarios**:

1. **Given** a document owner, **When** they open the "Share Document" dialog, select a specific user or department, and confirm, **Then** the document is granted shared access and the recipient receives an in-app notification.
2. **Given** a user who has received a shared document, **When** they navigate to "Shared with Me", **Then** the shared document is listed with the sharer's identity, share date, and access permissions.
3. **Given** a shared document, **When** the recipient attempts to modify the document metadata or delete the document, **Then** the system denies the action because they hold read-only shared permissions.
4. **Given** a new document uploaded to Project Alpha, **When** the upload completes, **Then** all active members of Project Alpha receive an in-app notification alerting them of the new document.

---

### User Story 5 - Task Integration and Dashboard Overview (Priority: P5)

As a project contributor working on assigned tasks, I want to attach documents directly to tasks and view recent documents on the main dashboard home page so that document workflows are seamlessly embedded into my daily routine.

**Why this priority**: Tight integration with tasks and the home dashboard increases user engagement and streamlines contextual work without requiring navigation across disconnected pages.

**Independent Test**: Can be fully tested by opening an existing task, uploading an attachment from the task view, verifying it links to both the task and its parent project, and checking that the dashboard home page displays the document in the "Recent Documents" widget.

**Acceptance Scenarios**:

1. **Given** a user viewing a task details page, **When** they upload a document through the task attachment section, **Then** the document is linked to the task and automatically associated with the task's parent project.
2. **Given** a user on the main dashboard home page, **When** the page renders, **Then** the "Recent Documents" widget displays their 5 most recently uploaded documents and the dashboard summary card reflects the total document count.
3. **Given** a user clicking a document item in the "Recent Documents" dashboard widget, **When** clicked, **Then** the user is taken directly to the document details or preview view.

---

### User Story 6 - Administrative Audit, Compliance, and Lifecycle Deletion (Priority: P6)

As a compliance administrator or manager, I want all document lifecycle events (upload, download, view, share, delete) to be immutably audited and want reporting tools to evaluate system activity and ensure regulatory compliance.

**Why this priority**: Enterprise document management mandates governance, accountability, and the prevention of unauthorized data exfiltration or unverified deletion.

**Independent Test**: Can be fully tested by performing upload, download, share, and delete actions, then accessing the Administrator audit log to verify that each event is recorded with timestamps, user identity, and action details.

**Acceptance Scenarios**:

1. **Given** an authorized user deleting their uploaded document, **When** they confirm the permanent deletion prompt, **Then** the document is deleted from storage and metadata records, and an audit event is recorded.
2. **Given** an Administrator accessing the Document Audit section, **When** viewing reports for a date range, **Then** they can inspect event history, filter by action type, and view document-type counts, top uploaders, and access patterns grouped by action and period.
3. **Given** a non-administrator user, **When** they attempt to access audit log views or compliance reports, **Then** the system denies access.

---

### Edge Cases

- **File Extension and Content Mismatch**: What happens when a user renames an executable or script file to `.pdf` or `.png`? The system must validate both file extension and MIME type against the strict whitelist and reject deceptive files.
- **Maximum File Size Exceeded**: What happens when a file exceeds 25 MB? The upload must be rejected prior to persistent storage, notifying the user of the exact limit.
- **Interrupted / Network Aborted Upload**: What happens if the network connection drops or the browser closes during an upload? The system must guarantee atomic operations: no orphaned physical files or half-created database records are left behind.
- **Duplicate File Names and Titles**: When multiple files share identical filenames or titles within the same category/project, the system retains unique internal storage keys (GUIDs) and database IDs to eliminate naming conflicts. The UI alerts the user of existing matches, offering them the choice to save as a new document or replace the existing file.
- **Direct Link / URL Parameter Tampering (IDOR Prevention)**: What happens if an unauthorized user attempts to download or view a document by guessing or modifying a document ID in a URL or request? The system must enforce service-level authorization and reject the request with an access denied response.
- **Deletion of Referenced Documents**: When a document attached to an active task is deleted, the system displays a confirmation prompt listing all affected tasks. Upon explicit user confirmation, the system automatically detaches the task references, purges the physical file, removes the document metadata, and logs an audit event.
- **Redundant Sharing**: What happens if a document owner attempts to share a document with someone who already has access (either directly or via project membership)? The system should inform the user that access is already granted without creating duplicate share entries.
- **Corrupted Document Preview**: What happens if an uploaded PDF or image file is corrupted and cannot be rendered by the previewer? The system must display a graceful fallback message indicating the preview is unavailable and offer a direct download option.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Users MUST be able to select and upload one or multiple files from their local system with visual progress and a per-file success or failure result. Each file MUST be processed independently; a failed file MUST NOT leave a database record or orphaned physical file, and MUST NOT roll back other successful files in the selection.
- **FR-002**: System MUST enforce a strict file type whitelist supporting only: PDF, Microsoft Word (`.doc`, `.docx`), Microsoft Excel (`.xls`, `.xlsx`), Microsoft PowerPoint (`.ppt`, `.pptx`), plain text (`.txt`), and images (`.jpg`, `.jpeg`, `.png`).
- **FR-003**: System MUST enforce a maximum file size limit of 25 MB per file and reject oversized files with clear, user-friendly error messages.
- **FR-004**: Users MUST provide a Document Title (required) and select a Category (required) from a predefined list: *Project Documents*, *Team Resources*, *Personal Files*, *Reports*, *Presentations*, *Other*. Duplicate titles within the same category or project are permitted (distinguished by ID and upload date), with the UI presenting an informational notice offering the option to replace the existing file or save as a new document.
- **FR-005**: Users MUST be able to optionally provide a Description, associate the document with a specific Project, and assign custom search Tags during or after upload.
- **FR-006**: System MUST automatically capture and store document metadata: upload timestamp, uploader identity, file size, and standard content MIME type (supporting at least 255 characters).
- **FR-007**: System MUST store physical files securely outside the public web root using unique non-predictable storage identifiers, preventing path traversal vulnerabilities and duplicate naming conflicts.
- **FR-008**: System MUST provide a "My Documents" view displaying all documents uploaded by the current user, showing title, category, upload date, file size, and associated project.
- **FR-009**: System MUST allow users to sort their document views by title, upload date, category, and file size.
- **FR-010**: System MUST allow users to filter document views by category, associated project, and upload date range. Both date boundaries are inclusive and compare to the document's UTC upload timestamp.
- **FR-011**: System MUST provide a "Project Documents" view accessible to all project team members and managers, displaying all documents associated with that project.
- **FR-012**: System MUST provide search capability querying title, description, tags, uploader name, and project name, returning only documents the user is authorized to view.
- **FR-013**: System MUST provide in-browser preview capability for standard PDF and image documents without requiring a local file download.
- **FR-014**: System MUST allow authorized users to securely download original documents with their original display filenames.
- **FR-015**: Document owners MUST be able to edit document metadata (title, description, category, tags) and replace the physical file with an updated version while preserving metadata.
- **FR-016**: Document owners and Project Managers (for project-associated documents) MUST be able to permanently delete documents after explicit confirmation. If a document is attached to one or more active tasks, the confirmation prompt MUST identify the attached tasks and, upon confirmation, automatically remove the document reference from those tasks before deletion.
- **FR-017**: Document owners MUST be able to share documents with specific users or departments, rendering shared documents in the recipient's "Shared with Me" view with read-only access. Department-level shares MUST be evaluated dynamically against the requesting user's current department profile at request time.
- **FR-018**: System MUST generate in-app notifications when a document is shared with a user or when a new document is added to a project they belong to (notifying all active project members and managers except the uploader).
- **FR-019**: Users viewing a task MUST be able to view and detach attached documents, attach an existing accessible document, and upload a new document directly from the task view. A new task upload MUST link the document to that task and automatically associate it with the task's parent project. All operations MUST enforce the user's task and document permissions.
- **FR-020**: System MUST display a "Recent Documents" widget on the dashboard home page displaying the user's 5 most recent documents, as well as a summary metric card showing total accessible document count.
- **FR-021**: System MUST strictly enforce role-based access boundaries:
  - *Employees*: Access their own documents, documents shared with them, and documents belonging to assigned projects.
  - *Team Leads*: Upload documents and view/download their own documents, documents belonging to their assigned projects, and documents uploaded by members of their department. Team Lead status alone does not grant edit, replacement, or deletion rights over another user's document.
  - *Project Managers*: Manage documents linked to projects they manage, including editing/replacing and deleting those project documents.
  - *Administrators*: Full access to inspect and oversee all documents and audit records across the organization.
- **FR-022**: System MUST independently verify user authorization for every data access and download operation, preventing Insecure Direct Object References (IDOR).
- **FR-023**: System MUST maintain an immutable audit trail capturing successful document upload, download, preview, share, edit/replacement, task attachment/detachment, and delete actions with user ID, timestamp, and action details, retained permanently without automated purging for full historical compliance.
- **FR-024**: Administrators MUST be able to view audit logs and generate summary reports that show document activity over a selected date range, popular document types, top uploaders, and access patterns grouped by action type and period. Reports MUST be restricted to Administrators.
- **FR-025**: System MUST guarantee atomic storage and data operations: if physical file persistence fails, no metadata record is created; if metadata creation fails, the physical file is purged.

### Key Entities

- **Document**:
  - `DocumentId`: Unique system identifier (integer).
  - `Title`: User-defined display title (required, string).
  - `Description`: Optional detailed summary (string).
  - `Category`: Categorization value from predefined set (required, string: "Project Documents", "Team Resources", "Personal Files", "Reports", "Presentations", "Other").
  - `OriginalFileName`: Original client filename including extension (string).
  - `StorageKey`: Internal unique identifier/path for physical file isolation (string).
  - `FileSize`: Size of the file in bytes (integer/long).
  - `ContentType`: Standard MIME type (string, up to 255 characters).
  - `CreatedDate`: Timestamp of initial upload in UTC (datetime).
  - `UpdatedDate`: Timestamp of the latest metadata edit or file replacement in UTC (datetime).
  - `UploadedByUserId`: Identifier of the user who uploaded the document.
  - `ProjectId`: Optional identifier of the project the document is associated with.
  - `TaskId`: Optional identifier of the task the document is attached to.
  - `Tags`: Comma-separated or collection of search tags (string/list).

- **DocumentShare**:
  - `DocumentShareId`: Unique identifier for the share relationship (integer).
  - `DocumentId`: Reference to the shared document.
  - `SharedWithUserId`: Reference to specific recipient user (optional if sharing with department).
  - `SharedWithDepartment`: Department identifier or name for group sharing (optional if sharing with user).
  - `SharedByUserId`: Reference to user granting access.
  - `SharedDate`: Timestamp when share was granted in UTC.
  - `Permission`: Level of access granted (default: Read-only).

- **DocumentAuditLog**:
  - `DocumentAuditLogId`: Unique identifier for the audit entry (integer).
  - `DocumentId`: Optional reference to the affected document; may be null so deletion history remains after the document record is removed.
  - `ActionType`: Action performed (e.g., Upload, Download, Preview, EditMetadata, ReplaceFile, Share, Delete).
  - `UserId`: Identifier of the user performing the action.
  - `Timestamp`: Precise timestamp of event occurrence.
  - `Details`: Contextual notes or metadata delta regarding the event.

- **Notification**:
  - `NotificationId`: Unique identifier for the alert.
  - `RecipientUserId`: User receiving the notification.
  - `Title`: Alert heading.
  - `Message`: Descriptive notification message.
  - `LinkUrl`: Direct navigation target to document or project.
  - `CreatedAt`: Creation timestamp.
  - `IsRead`: Status flag.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Document upload completes in under 30 seconds for files up to 25 MB under standard local and corporate network conditions.
- **SC-002**: Document lists and search queries return results and render on-screen in under 2 seconds for document volumes up to 500 records.
- **SC-003**: In-browser document preview loads and renders within 3 seconds for valid PDF and image documents.
- **SC-004**: Users can initiate and complete a document upload workflow in 3 clicks or fewer from the document management interface.
- **SC-005**: 100% of unauthorized document access or download attempts (via parameter manipulation or guessing identifiers) are blocked and rejected by service-level authorization.
- **SC-006**: At least 70% of distinct users with a successful login during the first three months after deployment upload at least one document during that same period. Calculate both groups from `LastLoginDate` and document upload timestamps at the end of the measurement window.
- **SC-007**: At least 90% of documents in a periodic human-reviewed sample are assigned a category that matches the document's purpose.
- **SC-008**: In timed usability sessions with representative dashboard users, the average time to locate and open a requested document is under 30 seconds.
- **SC-009**: 100% of failed or interrupted file uploads leave zero orphaned records in the database and zero orphaned files in physical storage.
- **SC-010**: Zero confirmed security incidents or unauthorized document disclosures are recorded in the post-launch security incident register during the measurement period.

## Scope Decisions

- Antivirus/malware scanning is not part of this delivery.
- An Azure storage provider or Azure deployment is not part of this delivery. The application continues to use the local filesystem provider.
- Performance thresholds, adoption, category accuracy, usability, and security-incident outcomes require measured validation or post-deployment evidence; a successful build alone does not satisfy them.
