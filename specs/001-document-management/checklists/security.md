# Security, Access Control & Compliance Checklist: Document Management

**Purpose**: Validate requirements quality, authorization boundaries, defense-in-depth, and compliance clarity for technical peer reviewers prior to Pull Request approval.  
**Created**: 2026-09-24  
**Feature**: [spec.md](file:///c:/Users/Davii/ContosoDashboard/specs/001-document-management/spec.md) | [plan.md](file:///c:/Users/Davii/ContosoDashboard/specs/001-document-management/plan.md)  
**Audience**: Technical Code Reviewers (Pull Request Approval)  
**Depth**: Standard (Balanced completeness, clarity, consistency, and edge cases)

> [!IMPORTANT]
> **Checklist Ownership & Evaluation Policy**:
> - This checklist represents **unit tests for requirements quality**. Items evaluate whether the requirements are complete, unambiguous, consistent, and verifiable.
> - Checking an item `[x]` indicates that the **reviewer** has verified that the requirement is well-defined and met by design.
> - Newly generated items MUST remain unchecked (`[ ]`). Automated tools do not check items off.

---

## 1. Service-Level Authorization & IDOR Defense-in-Depth

- [ ] CHK001 Are service-level authorization requirements explicitly documented for every document retrieval and mutation method rather than relying on UI page gates? [Completeness, Spec §FR-022, Constitution Principle III]
- [ ] CHK002 Is direct parameter manipulation (IDOR) rejection behavior specified with unambiguous status outcomes (e.g., 403 Forbidden vs 404 Not Found vs null)? [Clarity, Spec §FR-022, SC-005]
- [ ] CHK003 Are authorization rules consistently defined between document viewing and document streaming endpoints? [Consistency, Spec §FR-013, Spec §FR-014]
- [ ] CHK004 Does the specification define access control requirements for documents referenced indirectly via task associations? [Coverage, Spec §FR-019, Spec §FR-022]
- [ ] CHK005 Is cross-project document reassignment prohibited unless the requesting user possesses authorized rights in both source and destination projects? [Edge Case, Spec §FR-005, Spec §FR-015]

---

## 2. Role Boundaries & Least Privilege Segregation

- [ ] CHK006 Are Team Lead permission boundaries explicitly defined as read-only view/download over departmental files, unambiguously excluding edit, replace, or delete privileges over peers' files? [Clarity, Spec §FR-021, User Story 2]
- [ ] CHK007 Are Project Manager document management privileges strictly bounded to projects actively managed by that user? [Consistency, Spec §FR-011, Spec §FR-021]
- [ ] CHK008 Are Administrator privileges clearly distinguished between operational oversight and unrestricted document mutation? [Clarity, Spec §FR-024]
- [ ] CHK009 Does the spec specify what happens when an employee's department is updated in Active Directory / Claims regarding historical department shares? [Coverage, Dynamic Claims, Spec §FR-017]
- [ ] CHK010 Are shared recipient permissions documented as strictly immutable (read-only) unless an explicit higher project/ownership role exists? [Consistency, Spec §FR-017, Spec §FR-021]

---

## 3. Storage Safety, Validation Whitelisting & Atomicity

- [ ] CHK011 Is the maximum file size constraint quantified with exact byte thresholds (25 MB = 26,214,400 bytes) and exact rejection messages? [Clarity, Spec §FR-003, Constitution Principle IV]
- [ ] CHK012 Does the file validation specification enforce a dual-check whitelist (both file extension AND explicit MIME type), explicitly forbidding generic `application/octet-stream` or empty MIME bypasses? [Completeness, Spec §FR-002, Edge Cases]
- [ ] CHK013 Are non-negotiable physical path isolation rules documented ensuring uploaded files are stored strictly outside the public web root (`wwwroot`)? [Clarity, Spec §FR-007, Constitution Principle IV]
- [ ] CHK014 Are storage key generation requirements specified to use unpredictable GUIDs rather than user-supplied filenames to prevent path traversal? [Clarity, Spec §FR-007]
- [ ] CHK015 Are atomicity and cleanup requirements defined when database persistence fails after physical storage write, and vice-versa? [Completeness, Spec §FR-001, Spec §FR-025, SC-009]
- [ ] CHK016 Are requirements defined for partial failure handling during multi-file batch uploads ensuring successful files remain committed while failed files are purged? [Completeness, Spec §FR-001, Spec §FR-025]

---

## 4. Immutable Audit Logging & Regulatory Compliance

- [ ] CHK017 Are immutable audit logging requirements specified for all critical lifecycle events, explicitly including in-browser `Preview` and `Download` accesses? [Coverage, Spec §FR-023, User Story 6]
- [ ] CHK018 Are audit log data schema requirements defined with captured fields: actor User ID, timestamp in UTC, ActionType, and contextual details? [Completeness, Spec §FR-023, Data Model]
- [ ] CHK019 Are audit history persistence requirements documented to survive permanent document deletion (retaining log record with detached `DocumentId = null`)? [Edge Case, Spec §FR-016, Spec §FR-023]
- [ ] CHK020 Is access to organization-wide audit logs and compliance activity reports strictly restricted to the Administrator role? [Clarity, Spec §FR-024]
- [ ] CHK021 Are audit log search/report date range filters specified with inclusive UTC calendar-day boundary interpretation? [Consistency, Spec §FR-010, Spec §FR-024]

---

## 5. Lifecycle Deletion, Task Detachment & Cleanup

- [ ] CHK022 Does the specification define mandatory user confirmation prompting prior to permanent document deletion? [Clarity, Spec §FR-016]
- [ ] CHK023 Are requirements specified for identifying all active tasks referencing a document when deletion is requested? [Coverage, Spec §FR-016, Edge Cases]
- [ ] CHK024 Is automatic cascade detachment of task references defined before physical file removal and database entity deletion? [Consistency, Spec §FR-016]
- [ ] CHK025 Are requirements defined for verifying physical storage deletion and logging warnings if a file was missing from disk upon metadata deletion? [Edge Case, Recovery Flow, Constitution Principle IV]

---

## 6. Sharing Policies, Collaboration & Notification Privacy

- [ ] CHK026 Are redundant sharing prevention requirements documented when a document owner attempts to share with an existing project member? [Edge Case, Spec §FR-017]
- [ ] CHK027 Are notification requirements specified to explicitly exclude the uploading author from project upload alerts to eliminate alert spam? [Clarity, Spec §FR-018, User Story 4]
- [ ] CHK028 Are in-app notification payloads specified with direct target links to the associated document or project? [Completeness, Spec §FR-018]
- [ ] CHK029 Are department-level sharing resolution requirements specified without exposing non-departmental metadata across tenant boundaries? [Security, Spec §FR-017, Constitution Principle III]

---

## 7. Web Application Security Headers & Frame Sandboxing

- [ ] CHK030 Are HTTP security header requirements documented (specifically `X-Frame-Options: SAMEORIGIN` and CSP `frame-src 'self'`) to allow inline modal preview while preventing clickjacking? [Coverage, Security NFR, Spec §FR-013]
- [ ] CHK031 Are `Content-Disposition` response header requirements specified distinguishing inline streaming (`inline`) from forced attachments (`attachment; filename=...`)? [Clarity, Spec §FR-013, Spec §FR-014]
- [ ] CHK032 Are requirements documented ensuring streaming endpoints do not leak internal server storage paths or stack traces upon missing files? [Edge Case, Error Handling, Spec §FR-022]

---

## Notes for Reviewers

- Check items off (`[x]`) as each requirement quality criterion is evaluated and confirmed during Pull Request review.
- Any finding, ambiguity, or gap should be linked to the specific PR line or documented as an inline review comment.
- Traceability references link directly to sections in [`spec.md`](file:///c:/Users/Davii/ContosoDashboard/specs/001-document-management/spec.md) and [`constitution.md`](file:///c:/Users/Davii/ContosoDashboard/.specify/memory/constitution.md).
