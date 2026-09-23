# HTTP Streaming & Download API Contracts

**Feature**: `001-document-management`  
**Date**: 2026-09-23  

---

## 1. Stream Document (Preview)

- **Route**: `GET /api/documents/{id}/stream`
- **Authentication**: Required (Cookie-based auth)
- **Authorization**: Verifies requesting user has access to document `id` via `IDocumentService.AuthorizeAccessAsync` (IDOR check).
- **Responses**:
  - `200 OK`: File binary stream returned with headers:
    - `Content-Type`: Actual MIME type (e.g., `application/pdf`, `image/png`)
    - `Content-Disposition`: `inline; filename="[OriginalFileName]"`
  - `401 Unauthorized`: User is not authenticated.
  - `403 Forbidden`: User is not authorized to access this document.
  - `404 Not Found`: Document does not exist or physical file is missing.

---

## 2. Download Document

- **Route**: `GET /api/documents/{id}/download`
- **Authentication**: Required (Cookie-based auth)
- **Authorization**: Verifies requesting user has access to document `id` via `IDocumentService.AuthorizeAccessAsync` (IDOR check).
- **Responses**:
  - `200 OK`: File binary stream returned with headers:
    - `Content-Type`: `application/octet-stream` (or matching MIME type)
    - `Content-Disposition`: `attachment; filename="[OriginalFileName]"`
  - `401 Unauthorized`: User is not authenticated.
  - `403 Forbidden`: User is not authorized to download this document.
  - `404 Not Found`: Document does not exist or physical file is missing.
