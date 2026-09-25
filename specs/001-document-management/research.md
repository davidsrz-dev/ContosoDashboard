# Research & Decisions: Document Management Gap Closure

**Feature**: `001-document-management`
**Date**: 2026-09-24
**Status**: Decisions recorded for planning

This research is grounded in the current repository implementation and the user-confirmed scope decisions. No external service or package is required for the planned work.

## 1. Scope: Local Storage Only

**Decision**: Continue using `LocalFileStorageService` and local database providers. Do not add an Azure storage provider/deployment or antivirus/malware scanning in this delivery.

**Rationale**: The user explicitly excluded both items from the scope. Existing local storage, unique keys, validation, and authorization remain in force. The existing storage interface is retained as the application boundary; changes to support an actual cloud backend are not planned here.

## 2. Team Lead and Mutation Permissions

**Decision**: Team Leads may upload documents and view/download documents uploaded by members of their department and documents in assigned projects. Team Lead status alone does not grant edit, replace, or delete rights over another user's document. Apply the detailed operation rules: owner/Admin/responsible Project Manager can edit or replace; owner/Admin/responsible Project Manager can delete, with Project Manager rights limited to managed projects.

**Rationale**: The stakeholder file uses broad wording (“view/manage”), but the detailed edit/delete clauses identify narrower rights. The user selected applying the detailed rules. This keeps read access broad while making destructive operations explicit.

## 3. Multi-file Upload Semantics

**Decision**: Process every selected file independently and return one result per file. A failure does not roll back other successful files; a failed file leaves no record or physical file. Enforce type and 25 MB limit per file. Do not create a persistent batch entity.

**Rationale**: The data model is one `Document` per physical file and already supports atomic persistence per document. A transient result list gives the UI enough state without adding batch lifecycle complexity.

## 4. Date Range Semantics

**Decision**: Use inclusive UTC calendar dates for the upload-date filter. Translate an entered start date to the start of that UTC day and the entered end date to the beginning of the next UTC day (exclusive query bound). Missing bounds remain unbounded; a start after end is invalid.

**Rationale**: The existing upload timestamp (`CreatedDate`) is stored in UTC. This gives stable boundary behavior independent of host locale and avoids excluding uploads later on the selected end date.

## 5. Task-Context Upload

**Decision**: Reuse `DocumentUploadModel` and `IDocumentService.UploadDocumentAsync`, setting `TaskId` and the task's parent `ProjectId` from the task loaded by the server. Do not trust parent project IDs supplied only by the browser. Preserve attach-existing and detach behavior.

**Rationale**: `Document` already has both relationships, so no schema change is required. Server-derived association prevents a client from linking an upload to a project unrelated to the selected task.

## 6. Access Audit Events

**Decision**: Add a Download or Preview audit event only after document authorization succeeds and the storage stream has been resolved. Capture document, actor, UTC timestamp, original filename, and action type. A direct stream request is Preview; the attachment endpoint is Download.

**Rationale**: Both actions pass through `DocumentsController`, the common authorized boundary for binary delivery. Keeping audit creation behind `IDocumentService` preserves the existing service boundary. Existing denied-access warnings remain security logs, not successful access events.

## 7. Administrative Reports

**Decision**: Aggregate reports from existing `Document` and `DocumentAuditLog` records; do not persist a separate analytics warehouse. Report document types, top uploaders, and access patterns by action and time bucket, with a date range. Restrict report retrieval to Administrators in both UI and service layer.

**Rationale**: The training scale is approximately 500 documents and 50 users. Queries over the existing timestamped entities are sufficient to start at this scale and keep the source of truth singular; add indexes only if measurements show they are needed.

## 8. Success Metrics and Validation

- Upload, list/search, and preview thresholds are verified under documented local test conditions with dataset/file size and elapsed time recorded.
- For adoption, use `User.LastLoginDate` for users active in the first three months and distinct uploader IDs from `Document.CreatedDate` in that same window. The existing model already supplies both timestamps.
- Category accuracy requires a human review of a sample; it cannot be inferred from category population alone.
- Locate/open time requires timed usability sessions.
- Zero post-launch security incidents is an operational observation from the security incident register, not a code/build assertion.

## 9. Data and Dependency Impact

No database migration is expected. `Document` supports task/project relationships and UTC timestamps; `DocumentAuditLog` supports action/user/time aggregation; `User.LastLoginDate` supports adoption measurement. Add only transient DTOs and service methods needed for upload results, date filtering, and report aggregates. No new package is needed.
