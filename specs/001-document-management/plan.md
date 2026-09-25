# Implementation Plan: Document Management Gap Closure

**Branch**: `001-document-management` | **Date**: 2026-09-24 | **Spec**: `specs/001-document-management/spec.md`
**Input**: Existing implementation review, Stakeholder requirements, and clarified scope decisions.

## Summary

Complete the requirements that were specified but are missing or incomplete in the current implementation: multi-file upload with per-file outcomes, inclusive upload-date filtering, direct upload from task details, audit events for downloads and previews, administrative summary reports, and verifiable performance and adoption evidence. Preserve local/offline storage and the detailed role boundaries. Antivirus scanning and an Azure storage provider/deployment are explicitly excluded from this delivery.

This is a gap-closure plan for the existing document-management feature, not a replacement of its working upload, project, sharing, dashboard, or local storage workflows.

## Technical Context

**Language/Version**: C# / .NET 8.0 (`net8.0`)
**Primary Dependencies**: ASP.NET Core 8, Blazor Server, Entity Framework Core 8, Bootstrap 5.3
**Storage**: Existing local filesystem provider at `AppData/uploads`; relational metadata in the configured local database provider. Do not add a cloud storage provider in this scope.
**Testing**: Existing .NET test/build workflows; add focused automated coverage for file batches, date boundaries, role authorization, audit events, and report aggregates.
**Target Platform**: Cross-platform ASP.NET Core/Kestrel local training application.
**Project Type**: Blazor Server web application with authorized HTTP file streaming.
**Performance Goals**: Validate upload <=25 MB under 30 seconds; lists/search <=2 seconds at 500 seeded documents; PDF/image preview <=3 seconds. Report measurements with hardware/runtime and warm/cold-cache conditions.
**Constraints**: Offline-first; local file storage only; max 25 MB per file; allowlisted file extensions and MIME types; file writes outside `wwwroot`; service-level authorization and atomic file/metadata persistence. Team Leads can view/download department documents but do not gain edit/delete rights solely from that role.
**Scale/Scope**: Existing training scale of approximately 50 users and 500 documents. No antivirus scanning or Azure provider/deployment.

## Constitution Check

| Principle | Evaluation | Status |
|---|---|---|
| I. Offline-First with Cloud Migration Path | Keep local storage as the only provider in this plan. Retain the existing `IFileStorageService` boundary and its current documented migration path; no cloud package or provider is planned. | PASS |
| II. Infrastructure Abstraction & Dependency Injection | Batch processing, reports, date filtering, and audit persistence remain behind the existing document service and storage abstractions. | PASS |
| III. Service-Level Authorization | Every new batch item, task upload, audit query, report query, and date-filtered search must enforce the same document/task authorization in the service layer. | PASS |
| IV. Storage Safety & Atomic Operations | Each file in a batch follows validate → write unique physical file → save metadata; failure for one item leaves no orphan and does not roll back other successful items. | PASS |
| V. Spec-Driven Development & Quality Gates | The updated acceptance criteria map to focused tests and runnable validation scenarios before implementation tasks are regenerated. | PASS |

## Current-State Findings

- A single-file upload path, local storage, project association, existing-document task attachment/detachment, dashboard count/widget, and basic audit-log page are present.
- The upload form uses one file (`InputFile`), so FR-001 is only partially implemented.
- Search filters support category and project, but not the date range in FR-010.
- Task details can attach an existing accessible document but cannot upload a new file there, so FR-019 is incomplete.
- Authorized download and stream endpoints do not write Download/Preview audit entries.
- The admin audit page shows raw events but does not produce the aggregates required by FR-024.
- Performance, usability, categorization, and adoption criteria do not have reproducible evidence in the current quickstart.
- Team Lead read access exists; mutation permission is limited by the detailed owner/admin/project-manager rules.

## Design Decisions

1. **Multi-file processing is per-file**: validate and persist each selected file independently. Return a result for each file, including its original name and success/error message. A failed item is cleaned up; successful items remain committed.
2. **Date filter uses UTC calendar dates**: both selected dates are inclusive and compare with the existing UTC `CreatedDate`. Empty start/end values mean unbounded on that side. Start after end is rejected with a clear validation message.
3. **Task upload reuses the document upload flow**: task details pass the selected task ID and parent project ID into the existing upload service. Existing-document attach/detach remains supported. Task permissions are checked before upload or mutation.
4. **Audit successful access actions**: after authorization and storage stream lookup succeed, record one Download or Preview event with actor, document, UTC timestamp, and action details. Denied requests are not recorded as successful accesses; existing security logging remains separate.
5. **Reports are database aggregates**: group existing documents and audit events by MIME/extension, uploader, action, and time bucket. Do not duplicate raw audit data in a second report table.
6. **Measure operational criteria with existing signals where possible**: upload records provide upload numerator; `User.LastLoginDate` defines the active-user denominator at the end of the three-month window. Category accuracy and find/open time require human review/usability sessions; zero incidents is verified against the operational incident register.
7. **No external scanning/cloud services**: antivirus/malware scanning and Azure provider/deployment are out of scope; retain current extension/MIME/size validation and local storage.

## Project Structure

```text
specs/001-document-management/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
└── contracts/
    ├── service-contracts.md
    └── http-api-contracts.md

ContosoDashboard/
├── Controllers/DocumentsController.cs       # Access-event audit for stream/download
├── Models/DocumentViewModels.cs             # Per-file upload results and report DTOs
├── Pages/Documents.razor                    # Multi-file selector and date filters
├── Pages/Tasks.razor                        # Task-scoped upload and attachment list
├── Services/DocumentService.cs              # Batch, filter, permission, audit and report logic
└── Shared/RecentDocumentsWidget.razor       # Existing dashboard integration
```

No new database entity is currently needed: documents, audit events, user roles, and `LastLoginDate` already provide the required source data. Confirm this against the existing model during implementation; add a migration only if an actual schema gap is found.

## Implementation Phases

### Phase A — Contract and permission alignment

- Align service/API contracts with the updated spec and expose only the required operations.
- Keep Team Lead department access read-only for other users' documents; enforce owner, Administrator, and responsible Project Manager mutation rights consistently in UI and service layer.
- Define a task-upload authorization rule consistent with existing task assignment/project membership checks.

### Phase B — Multi-file upload and task-context upload

- Accept one or more files and show independent progress/completion/error state per file.
- Enforce 25 MB and allowlist validation per file before persistence.
- Reuse atomic storage persistence for each file; partial batch successes remain available.
- Add upload control to task details, pre-associate the task and parent project, and refresh attached documents after completion.

### Phase C — Search date range

- Add inclusive start/end date controls to document filters.
- Apply UTC date bounds in the service query, alongside existing authorization, category, project, and search filters.
- Cover empty, single-bound, exact-boundary, and invalid reversed ranges.

### Phase D — Audit access and administrative reports

- Record Preview and Download access after successful authorization and stream resolution.
- Keep audit records immutable and retained; include task attach/detach and current lifecycle events.
- Add Administrator-only summary views for document type counts, top uploaders, and access patterns by action/date.
- Test that non-admin users cannot retrieve organization-wide logs or reports.

### Phase E — Verification and success measurement

- Extend quickstart with role scenarios, partial batch failures, date boundaries, task uploads, access auditing, and report output.
- Measure upload/list/search/preview thresholds against documented local test conditions.
- Measure three-month adoption using successful logins and uploads from the same window; conduct human category review and timed find/open usability sessions.
- Record post-launch security incidents from the operational incident register; do not represent build/test success as proof of zero incidents.

## Quality Gates

- Every functional requirement maps to at least one automated test or explicit manual acceptance scenario.
- No unauthorized user can gain access through a batch item ID, task ID, report route, or modified document ID.
- Each file in a batch is independently atomic and produces a deterministic result.
- Download and preview events appear in audit logs after successful access; reports reconcile with those logs.
- Performance evidence includes dataset size, file size, elapsed time, and environment details.

## Complexity Tracking

| Potential complexity | Decision | Rationale |
|---|---|---|
| Persisting upload-batch entities | Not planned | Per-file outcomes can be returned to the current UI; batch history is not required. |
| Separate report warehouse | Not planned | Current local scale supports aggregation from `Document` and `DocumentAuditLog`. |
| Antivirus service or cloud provider | Excluded | Explicit user scope decision. |
