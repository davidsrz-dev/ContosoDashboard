<!--
Sync Impact Report:
- Version change: 0.0.0 (template placeholder) → 1.0.0
- List of modified principles:
  - [PRINCIPLE_1_NAME] → I. Offline-First with Cloud Migration Path
  - [PRINCIPLE_2_NAME] → II. Strict Infrastructure Abstraction & Dependency Injection
  - [PRINCIPLE_3_NAME] → III. Defense-in-Depth & Service-Level Authorization (IDOR Prevention)
  - [PRINCIPLE_4_NAME] → IV. Storage Safety & Atomic Data Operations
  - [PRINCIPLE_5_NAME] → V. Spec-Driven Development & Test-First Quality Gates
- Added sections:
  - Technical Stack & Architectural Constraints (formerly [SECTION_2_NAME])
  - Development Workflow & Quality Gates (formerly [SECTION_3_NAME])
  - Governance & Compliance Policy (formerly [SECTION_4_NAME])
- Removed sections: None
- Follow-up TODOs: None
-->

# ContosoDashboard Constitution

## Core Principles

### I. Offline-First with Cloud Migration Path
The application MUST remain fully functional in offline, local development environments without requiring active cloud connections, external subscriptions, or third-party paid services.
- All core training features MUST operate against local providers (e.g., SQL Server LocalDB, local filesystem storage, cookie-based mock authentication).
- Every feature MUST provide a direct, documented migration path to Azure cloud services (e.g., Azure SQL Database, Azure Blob Storage, Microsoft Entra ID).
- Cloud transitions MUST NOT alter core business rules, UI logic, or service boundaries.
- Rationale: ContosoDashboard is an educational reference application where local availability, zero external service dependency, and clean cloud migration demonstration are foundational requirements.

### II. Strict Infrastructure Abstraction & Dependency Injection
All infrastructure dependencies (databases, physical file storage, identity providers, and notifications) MUST be encapsulated behind clean interface abstractions.
- Business services, Blazor components, and controllers MUST interact strictly with service interfaces (e.g., `IFileStorageService`, `INotificationService`, `ITaskService`) rather than concrete IO implementations.
- Transitioning between local implementations and cloud implementations MUST be achievable solely through Dependency Injection configuration in `Program.cs`.
- Concrete implementations MUST NOT leak implementation-specific types or exceptions into domain layers.
- Rationale: Decoupling infrastructure from business logic guarantees maintainability, modularity, unit testability, and seamless environment portability.

### III. Defense-in-Depth & Service-Level Authorization (IDOR Prevention)
Security and access control MUST NOT rely solely on UI routing, navigation controls, or page-level attributes (`[Authorize]`).
- Every service-layer method performing data retrieval or mutation MUST independently authenticate and authorize the requesting user (`requestingUserId`).
- Insecure Direct Object References (IDOR) MUST be proactively prevented: users MUST NOT be able to view or manipulate records by guessing or altering URL identifiers.
- User and tenant isolation MUST be strictly enforced: employees can only view or modify resources they own or have explicit membership/role permissions to access.
- Rationale: Defense-in-depth ensures that circumventing UI controls or manipulating direct entity IDs cannot lead to unauthorized data exposure or tampering.

### IV. Storage Safety & Atomic Data Operations
All physical file operations and data mutations MUST guarantee integrity, safe ordering, and protection against orphaned records or path traversal attacks.
- File storage operations MUST follow the non-negotiable sequence: generate unique path (using GUID) → persist file to storage outside `wwwroot` → persist database record.
- Uploaded files MUST be validated against strict whitelists for MIME type and file extension, and adhere to size constraints (max 25 MB).
- User-supplied filenames MUST NEVER be used as physical storage paths; all downloads MUST pass through authorized endpoints.
- If physical storage fails, database transactions MUST NOT be committed; if database persistence fails, uploaded files MUST be cleaned up.
- Rationale: Prevents path traversal vulnerabilities, duplicate key collisions, orphaned files on disk, and database desynchronization.

### V. Spec-Driven Development & Test-First Quality Gates
All feature additions and substantial refactoring MUST follow the Spec-Driven Development (SDD) methodology via GitHub Spec Kit.
- Non-trivial features MUST proceed through formal specification (`spec.md`), architectural planning (`plan.md`), and ordered tasks (`tasks.md`) before implementation.
- Service contracts, authorization boundaries, and core workflows MUST be validated with automated tests and verifiable acceptance criteria.
- Red-Green-Refactor cycles MUST be maintained; pull requests without corresponding test coverage for business logic and access control will be rejected.
- Rationale: Prevents scope creep, enforces rigorous design before code generation, and guarantees enduring quality and architectural integrity.

## Technical Stack & Architectural Constraints
The ContosoDashboard architecture follows defined technical guardrails to maintain simplicity, consistency, and training effectiveness.
- **Framework & UI**: ASP.NET Core 8.0 with Blazor Server, utilizing Razor components and Bootstrap 5.3 with Bootstrap Icons for responsive presentation.
- **Data & Persistence**: Entity Framework Core with SQL Server LocalDB for offline environments; models and contexts designed for zero-code migration to Azure SQL Database.
- **Security & Identity**: Cookie-based mock authentication for training with claims-based identity; role-based access control with standard policies (`Employee`, `TeamLead`, `ProjectManager`, `Administrator`); strict HTTP security headers (CSP, X-Frame-Options, X-Content-Type-Options, HSTS).
- **Project Structure**: Strict separation of concerns across `Models`, `Data` (`ApplicationDbContext`), `Services`, and `Pages`/`Shared`.

## Development Workflow & Quality Gates
Development within ContosoDashboard adheres to a disciplined lifecycle governed by automated checks and peer reviews.
- **Specification Gate**: Feature development MUST begin with Spec Kit workflows (`/speckit-specify`, `/speckit-plan`, `/speckit-tasks`).
- **Peer Review & Architectural Compliance**: Every pull request MUST undergo review against the 5 Core Principles, with specific verification of service-level authorization checks and interface abstractions.
- **Verification Gate**: All existing and new automated tests MUST pass cleanly. Code changes must build without warnings or unresolved security alerts.

## Governance
This Constitution constitutes the definitive architectural standard for ContosoDashboard and supersedes informal conventions or ad-hoc practices.
- **Supremacy & Adherence**: All contributions, architectural proposals, and code modifications MUST comply with the principles and constraints established herein.
- **Amendment Procedure**: Amendments to this Constitution require documented technical rationale, review, and explicit approval. Any breaking change to principles mandates an impact analysis and migration plan.
- **Versioning Policy**: The Constitution follows semantic versioning:
  - MAJOR (`X.0.0`): Incompatible changes, removal of existing principles, or fundamental restructuring of governance.
  - MINOR (`0.X.0`): Addition of new principles or significant expansion of architectural constraints.
  - PATCH (`0.0.X`): Clarifications, phrasing improvements, typographical fixes, and non-semantic refinements.
- **Compliance Review**: Regular audits during pull requests ensure adherence; complexity or departures from principles MUST be formally justified.

**Version**: 1.0.0 | **Ratified**: 2026-09-23 | **Last Amended**: 2026-09-23
