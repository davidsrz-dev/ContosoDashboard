using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ContosoDashboard.Services;

namespace ContosoDashboard.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentService _documentService;
    private readonly IFileStorageService _fileStorageService;
    private readonly ILogger<DocumentsController> _logger;

    public DocumentsController(
        IDocumentService documentService,
        IFileStorageService fileStorageService,
        ILogger<DocumentsController> logger)
    {
        _documentService = documentService;
        _fileStorageService = fileStorageService;
        _logger = logger;
    }

    private int GetCurrentUserId()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(idClaim, out var userId))
        {
            return userId;
        }
        return 0;
    }

    /// <summary>
    /// Streams document for in-browser inline preview (PDF and images).
    /// </summary>
    [HttpGet("{id}/stream")]
    public async Task<IActionResult> StreamDocument(int id)
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized();

        var isAuthorized = await _documentService.AuthorizeAccessAsync(id, userId);
        if (!isAuthorized)
        {
            _logger.LogWarning("IDOR Attempt: User {UserId} attempted unauthorized stream access to document {DocumentId}", userId, id);
            return Forbid();
        }

        var document = await _documentService.GetDocumentByIdAsync(id, userId);
        if (document == null) return NotFound();

        var stream = await _fileStorageService.GetFileStreamAsync(document.StorageKey);
        if (stream == null) return NotFound("Physical file not found in storage.");

        await _documentService.RecordDocumentAccessAsync(id, userId, "Preview");

        Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
        Response.Headers["Content-Disposition"] = $"inline; filename=\"{Uri.EscapeDataString(document.OriginalFileName)}\"";
        return File(stream, document.ContentType);
    }

    /// <summary>
    /// Downloads document with original filename attachment disposition.
    /// </summary>
    [HttpGet("{id}/download")]
    public async Task<IActionResult> DownloadDocument(int id)
    {
        var userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized();

        var isAuthorized = await _documentService.AuthorizeAccessAsync(id, userId);
        if (!isAuthorized)
        {
            _logger.LogWarning("IDOR Attempt: User {UserId} attempted unauthorized download of document {DocumentId}", userId, id);
            return Forbid();
        }

        var document = await _documentService.GetDocumentByIdAsync(id, userId);
        if (document == null) return NotFound();

        var stream = await _fileStorageService.GetFileStreamAsync(document.StorageKey);
        if (stream == null) return NotFound("Physical file not found in storage.");

        await _documentService.RecordDocumentAccessAsync(id, userId, "Download");

        return File(stream, document.ContentType, document.OriginalFileName);
    }
}
