# Implementation Plan: Document Upload and Management

**Branch**: `001-document-management` | **Date**: 2026-09-23 | **Spec**: [specs/001-document-management/spec.md](file:///c:/Users/Davii/ContosoDashboard/specs/001-document-management/spec.md)  
**Input**: Feature specification from `specs/001-document-management/spec.md`

---

## Summary

Implement secure document upload, organization, and management capabilities within ContosoDashboard. The technical approach introduces an abstract file storage layer (`IFileStorageService`) with a local implementation (`LocalFileStorageService`) storing files outside `wwwroot` (`AppData/uploads`), a business service (`IDocumentService`) providing defense-in-depth authorization against IDOR vulnerabilities, Entity Framework Core data persistence (`Document`, `DocumentShare`, `DocumentAuditLog`), an HTTP streaming controller for in-browser PDF/image preview, and Blazor Server components for document management, project views, task attachments, and home dashboard widgets.

---

## Technical Context

**Language/Version**: C# / .NET 8.0 (`net8.0`)  
**Primary Dependencies**: ASP.NET Core 8.0, Microsoft.EntityFrameworkCore 8.0, Microsoft.EntityFrameworkCore.SqlServer 8.0, Blazor Server, Bootstrap 5.3, Bootstrap Icons  
**Storage**: SQL Server LocalDB (`ApplicationDbContext`), local filesystem (`AppData/uploads`) outside `wwwroot` with GUID-based relative paths; designed for zero-code configuration swap to Azure Blob Storage and Azure SQL  
**Testing**: .NET CLI (`dotnet test`), MSTest/xUnit unit and contract tests  
**Target Platform**: Cross-platform web application on ASP.NET Core Kestrel (Windows / Linux)  
**Project Type**: Web Application (ASP.NET Core with Blazor Server + Razor Pages + Streaming Controller)  
**Performance Goals**: File upload < 30s for files up to 25 MB; document list/search rendering < 2s for 500 records; PDF/image preview rendering < 3s  
**Constraints**: Fully offline-capable (no external paid cloud required for training), IDOR prevention via service-level authorization (`requestingUserId`), strict storage sequencing (GUID path → physical write → DB save; purge on error), strict MIME and extension whitelist  
**Scale/Scope**: Contoso internal employee portal (~500 documents in training environment, ~50 users)  

---

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Evaluation | Status |
|---|---|---|
| **I. Offline-First with Cloud Migration Path** | Uses LocalDB and local filesystem outside `wwwroot` with clean `IFileStorageService` abstraction; enables seamless swap to Azure Blob Storage and Azure SQL. | **PASS** |
| **II. Strict Infrastructure Abstraction & Dependency Injection** | All file I/O and document business logic are encapsulated behind `IFileStorageService` and `IDocumentService`, configured via standard DI in `Program.cs`. | **PASS** |
| **III. Defense-in-Depth & Service-Level Authorization (IDOR Prevention)** | Every `DocumentService` method checks `requestingUserId` against ownership, project memberships, department claims, and administrative roles. | **PASS** |
| **IV. Storage Safety & Atomic Data Operations** | Files are stored using GUID paths outside `wwwroot`, physical writes precede DB transactions, files are purged on DB failure, and user filenames are never used as filesystem paths. | **PASS** |
| **V. Spec-Driven Development & Test-First Quality Gates** | Full specification (`spec.md`), technical plan (`plan.md`), data model (`data-model.md`), contracts (`contracts/`), and quickstart validation guide (`quickstart.md`) defined prior to implementation. | **PASS** |

---

## Project Structure

### Documentation (this feature)

```text
specs/001-document-management/
├── plan.md              # Technical plan (this file)
├── research.md          # Storage & architecture decisions (Phase 0 output)
├── data-model.md        # Entity definitions & relationships (Phase 1 output)
├── quickstart.md        # End-to-end verification guide (Phase 1 output)
├── contracts/           # Service & HTTP API interface contracts (Phase 1 output)
│   ├── service-contracts.md
│   └── http-api-contracts.md
└── checklists/
    └── requirements.md  # Specification quality checklist
```

### Source Code Layout

```text
ContosoDashboard/
├── Controllers/
│   └── DocumentsController.cs       # Authorized streaming and download endpoints
├── Data/
│   ├── ApplicationDbContext.cs      # EF Core DbContext with Document DbSets and model configurations
│   └── DbInitializer.cs             # Seed data for document categories and sample documents
├── Models/
│   ├── Document.cs                  # Main document metadata entity
│   ├── DocumentShare.cs             # Sharing relationship entity (user/department)
│   ├── DocumentAuditLog.cs          # Audit trail entity
│   └── DocumentViewModels.cs        # Upload, edit, and filter view models
├── Pages/
│   ├── Documents.razor              # Main Document Management page (My Documents, Projects, Shared)
│   ├── Documents.razor.cs           # Code-behind logic for document management
│   ├── DocumentDetails.razor       # Document inspection, metadata editing, and sharing dialog
│   └── DocumentPreviewModal.razor   # In-browser preview modal for PDF and image streams
├── Services/
│   ├── IFileStorageService.cs       # Storage interface abstraction
│   ├── LocalFileStorageService.cs  # Local filesystem implementation (AppData/uploads)
│   ├── IDocumentService.cs          # Document business logic and IDOR authorization interface
│   └── DocumentService.cs           # Implementation of IDocumentService
├── Shared/
│   ├── NavMenu.razor                # Added navigation item for Documents
│   └── RecentDocumentsWidget.razor  # Home dashboard widget for 5 recent documents
└── Program.cs                       # DI registration for IFileStorageService and IDocumentService
```

**Structure Decision**: Integrated within the existing ASP.NET Core Blazor Server project (`ContosoDashboard`), adhering to the established `Models`, `Data`, `Services`, `Pages`, and `Shared` folder conventions.

---

## Complexity Tracking

*No violations or unnecessary architectural complexity. All designs comply strictly with ContosoDashboard Constitution principles.*

---

## Phases & Deliverables

### Phase 0: Outline & Research
- Resolved all architectural unknowns regarding storage paths, security streaming, and IDOR validation in [research.md](file:///c:/Users/Davii/ContosoDashboard/specs/001-document-management/research.md).

### Phase 1: Design & Contracts
- **Data Model**: Comprehensive database entity schemas, relationships, and lifecycle rules in [data-model.md](file:///c:/Users/Davii/ContosoDashboard/specs/001-document-management/data-model.md).
- **Contracts**: Interface signatures for `IFileStorageService`, `IDocumentService`, and HTTP streaming endpoints in [contracts/](file:///c:/Users/Davii/ContosoDashboard/specs/001-document-management/contracts/).
- **Quickstart Guide**: Step-by-step verification flows for all user roles and edge cases in [quickstart.md](file:///c:/Users/Davii/ContosoDashboard/specs/001-document-management/quickstart.md).

### Phase 2: Tasks & Implementation (Next Step)
- Generate dependency-ordered implementation tasks in `tasks.md` via `/speckit-tasks`.
- Execute implementation via `/speckit-implement`.
