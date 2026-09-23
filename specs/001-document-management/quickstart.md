# Quickstart Validation Guide: Document Upload and Management

**Feature**: `001-document-management`  
**Date**: 2026-09-23  

This guide provides end-to-end verification workflows to validate the document upload and management feature across all user stories and acceptance criteria.

---

## Prerequisites

1. .NET 8.0 SDK installed.
2. SQL Server LocalDB running locally.
3. ContosoDashboard project running on `http://localhost:5000` (or configured HTTPS port).

---

## Setup & Execution Commands

```powershell
# 1. Navigate to the project directory
cd ContosoDashboard

# 2. Run the application (LocalDB database automatically initialized and seeded)
dotnet run
```

Open a browser and navigate to `http://localhost:5000`.

---

## Validation Scenarios

### Scenario 1: Personal Document Upload & Preview (P1 / MVP)
1. **Login**: Select **"Ni Kang"** (Employee) from `/login` and submit.
2. **Navigate**: Go to the **Documents** navigation link (`/documents`).
3. **Upload**: Click **"Upload Document"**:
   - File: Select a valid PDF file (< 25 MB).
   - Title: `My Personal Goals.pdf`.
   - Category: `Personal Files`.
   - Submit the form.
4. **Verify List**: Verify that `My Personal Goals.pdf` appears immediately in the "My Documents" tab with status, size, and upload timestamp.
5. **Verify Preview**: Click the **Preview** button. A modal should display rendering the PDF inline via `/api/documents/{id}/stream`.
6. **Verify Download**: Click **Download**. Verify the file downloads with the original filename.

### Scenario 2: Project Document Sharing & IDOR Protection (P2)
1. **Upload Project Document**: While logged in as Ni Kang, upload a document:
   - Title: `Engineering Architecture v1.docx`.
   - Category: `Project Documents`.
   - Associated Project: `Contoso E-Commerce Redesign` (a project where Ni Kang is a member).
2. **Verify Project Member Access**:
   - Logout and login as **"Floris Kregel"** (Team Lead, also assigned to that project).
   - Navigate to `/projects`, select `Contoso E-Commerce Redesign`, and open the **Documents** tab.
   - Verify `Engineering Architecture v1.docx` is visible and downloadable.
3. **Verify IDOR Protection**:
   - Copy the document ID or direct stream link `http://localhost:5000/api/documents/{id}/download`.
   - Logout and login as a user who is NOT a member of that project (or unauthenticated incognito window).
   - Attempt to access `/api/documents/{id}/download`.
   - **Expected Result**: HTTP `403 Forbidden` or redirect to `/login`.

### Scenario 3: Task Attachment & Cascade Detachment (P3 & Clarification)
1. **Attach to Task**:
   - Navigate to `/tasks`, open a task assigned to Ni Kang.
   - In the "Attachments" section, click "Attach Document" and select `Engineering Architecture v1.docx`.
   - Verify the document is displayed under task attachments.
2. **Delete with Warning**:
   - Navigate back to `/documents`.
   - Attempt to delete `Engineering Architecture v1.docx`.
   - Verify the confirmation modal lists the attached task.
   - Confirm deletion.
   - Verify the document is removed from `/documents` and the reference is detached from the task without errors.

### Scenario 4: Oversized or Invalid File Rejection (Edge Case)
1. Attempt to upload a `.exe` or `.zip` file.
   - **Expected Result**: UI displays validation error: *"Unsupported file format"*.
2. Attempt to upload a file larger than 25 MB.
   - **Expected Result**: UI displays error: *"File exceeds maximum allowed size of 25 MB"*.
