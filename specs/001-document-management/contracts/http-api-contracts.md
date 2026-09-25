# HTTP Streaming & Download API Contracts

**Feature**: `001-document-management`
**Date**: 2026-09-24
**Status**: Existing routes plus audit behavior

## 1. Stream Document (Preview)

- **Route**: `GET /api/documents/{id}/stream`
- **Authentication**: Required (cookie-based authentication).
- **Authorization**: `IDocumentService.AuthorizeAccessAsync` MUST authorize the requesting user for the document before bytes are returned.
- **Successful response**: `200 OK`, binary stream, actual `Content-Type`, and inline content disposition using the original filename.
- **Audit side effect**: Record one `Preview` event only after authorization succeeds and the storage stream is found.
- **Errors**: `401 Unauthorized`, `403 Forbidden`, or `404 Not Found` when the document/file is unavailable. Failed/denied requests are not counted as successful previews.

## 2. Download Document

- **Route**: `GET /api/documents/{id}/download`
- **Authentication**: Required (cookie-based authentication).
- **Authorization**: `IDocumentService.AuthorizeAccessAsync` MUST authorize the requesting user for the document before bytes are returned.
- **Successful response**: `200 OK`, binary stream, actual `Content-Type`, and attachment disposition using the original filename.
- **Audit side effect**: Record one `Download` event only after authorization succeeds and the storage stream is found.
- **Errors**: `401 Unauthorized`, `403 Forbidden`, or `404 Not Found` when the document/file is unavailable. Failed/denied requests are not counted as successful downloads.

## 3. UI-Only Service Workflows

The existing Blazor Server application invokes service methods for multi-file upload, task attachment/upload, date filtering, audit history, and Administrator reports. These workflows do not require new public HTTP endpoints unless a later client/API requirement is approved.

- Multi-file upload returns per-file outcomes; a request-level validation failure must not conceal which files succeeded.
- Task upload derives `TaskId` and parent `ProjectId` from the authorized task loaded server-side.
- Date filters use inclusive UTC calendar dates; reversed ranges return a validation error.
- Audit and report queries are Administrator-only at the service boundary as well as in the UI.
