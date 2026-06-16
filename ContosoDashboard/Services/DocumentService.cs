using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ContosoDashboard.Data;
using ContosoDashboard.Models;
using ContosoDashboard.Services.Storage;

namespace ContosoDashboard.Services;

/// <summary>
/// Orchestrates document upload and management and is the single authorization boundary for
/// documents. Shared helpers (category list, validation, the permission-scoped accessible
/// query, and activity logging) live here; story-specific operations are implemented in
/// their respective feature phases.
/// </summary>
public class DocumentService : IDocumentService
{
    private readonly ApplicationDbContext _context;
    private readonly IFileStorageService _fileStorage;
    private readonly IMalwareScanner _malwareScanner;
    private readonly INotificationService _notificationService;
    private readonly DocumentStorageOptions _options;
    private readonly ILogger<DocumentService> _logger;

    public DocumentService(
        ApplicationDbContext context,
        IFileStorageService fileStorage,
        IMalwareScanner malwareScanner,
        INotificationService notificationService,
        IOptions<DocumentStorageOptions> options,
        ILogger<DocumentService> logger)
    {
        _context = context;
        _fileStorage = fileStorage;
        _malwareScanner = malwareScanner;
        _notificationService = notificationService;
        _options = options.Value;
        _logger = logger;
    }

    // ---- Shared helpers (used across feature phases) ------------------------------------

    /// <summary>
    /// Validates a candidate upload against the configured size limit and extension/MIME
    /// whitelist. Returns null when valid, or a clear error message when rejected.
    /// </summary>
    protected string? ValidateFile(string fileName, string contentType, long fileSizeBytes)
    {
        if (fileSizeBytes <= 0)
        {
            return "The file is empty.";
        }

        if (fileSizeBytes > _options.MaxFileSizeBytes)
        {
            var maxMb = _options.MaxFileSizeBytes / (1024 * 1024);
            return $"The file exceeds the maximum size of {maxMb} MB.";
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(extension) ||
            (_options.AllowedExtensions.Count > 0 && !_options.AllowedExtensions.Contains(extension)))
        {
            return $"The file type '{extension}' is not supported.";
        }

        if (_options.AllowedContentTypes.Count > 0 &&
            !_options.AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            return "The file content type is not supported.";
        }

        return null;
    }

    /// <summary>Returns true when the category is one of the predefined values.</summary>
    protected static bool IsValidCategory(string category) => DocumentCategories.All.Contains(category);

    /// <summary>
    /// Builds the permission-scoped set of documents a user may access:
    /// own uploads ∪ documents of projects they manage/belong to ∪ documents shared directly
    /// or via their team ∪ elevated-role visibility (Team Lead → team members' documents,
    /// Project Manager → their projects' documents, Administrator → all).
    /// </summary>
    protected async Task<IQueryable<Document>> GetAccessibleDocumentsAsync(int requestingUserId)
    {
        var user = await _context.Users.FindAsync(requestingUserId);
        if (user is null)
        {
            return _context.Documents.Where(_ => false);
        }

        var department = user.Department;
        var isAdmin = user.Role == UserRole.Administrator;
        var isProjectManager = user.Role == UserRole.ProjectManager;
        var isTeamLead = user.Role == UserRole.TeamLead;

        var managedProjectIds = _context.Projects
            .Where(p => p.ProjectManagerId == requestingUserId)
            .Select(p => p.ProjectId);

        var memberProjectIds = _context.ProjectMembers
            .Where(pm => pm.UserId == requestingUserId)
            .Select(pm => pm.ProjectId);

        var accessibleProjectIds = managedProjectIds.Union(memberProjectIds);

        return _context.Documents.Where(d =>
            isAdmin
            || d.UploadedByUserId == requestingUserId
            || (d.ProjectId != null && accessibleProjectIds.Contains(d.ProjectId.Value))
            || d.Shares.Any(s =>
                    s.SharedWithUserId == requestingUserId
                    || (s.SharedWithTeam != null && department != null && s.SharedWithTeam == department))
            || (isProjectManager && d.ProjectId != null && managedProjectIds.Contains(d.ProjectId.Value))
            || (isTeamLead && department != null && d.UploadedByUser.Department == department));
    }

    /// <summary>Determines whether a user may edit/delete a document (uploader, owning PM, or admin).</summary>
    protected async Task<bool> CanManageAsync(Document document, int requestingUserId)
    {
        if (document.UploadedByUserId == requestingUserId)
        {
            return true;
        }

        var user = await _context.Users.FindAsync(requestingUserId);
        if (user is null)
        {
            return false;
        }

        if (user.Role == UserRole.Administrator)
        {
            return true;
        }

        if (document.ProjectId.HasValue)
        {
            var isOwningProjectManager = await _context.Projects
                .AnyAsync(p => p.ProjectId == document.ProjectId.Value && p.ProjectManagerId == requestingUserId);
            if (isOwningProjectManager)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Writes an audit entry for a document action.</summary>
    protected async Task LogActivityAsync(int? documentId, string action, int performedByUserId, string? details = null)
    {
        _context.DocumentActivityLogs.Add(new DocumentActivityLog
        {
            DocumentId = documentId,
            Action = action,
            PerformedByUserId = performedByUserId,
            Timestamp = DateTime.UtcNow,
            Details = details
        });
        await _context.SaveChangesAsync();
    }

    /// <summary>True when the user manages or is a member of the given project.</summary>
    protected async Task<bool> IsProjectMemberOrManagerAsync(int projectId, int userId)
    {
        var isManager = await _context.Projects
            .AnyAsync(p => p.ProjectId == projectId && p.ProjectManagerId == userId);
        if (isManager)
        {
            return true;
        }

        return await _context.ProjectMembers
            .AnyAsync(pm => pm.ProjectId == projectId && pm.UserId == userId);
    }

    // ---- Story operations (implemented in their feature phases) -------------------------

    public async Task<Document> UploadAsync(DocumentUploadRequest request, int requestingUserId)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new ArgumentException("A document title is required.");
        }

        if (!IsValidCategory(request.Category))
        {
            throw new ArgumentException("A valid category is required.");
        }

        // Validate the file against size limit and extension/MIME whitelist.
        var validationError = ValidateFile(request.FileName, request.ContentType, request.FileSizeBytes);
        if (validationError is not null)
        {
            throw new InvalidOperationException(validationError);
        }

        // Authorize project membership when uploading to a project.
        if (request.ProjectId.HasValue &&
            !await IsProjectMemberOrManagerAsync(request.ProjectId.Value, requestingUserId))
        {
            throw new UnauthorizedAccessException("You are not a member of the selected project.");
        }

        // Scan before storage; reject (and persist nothing) if not clean.
        if (request.Content.CanSeek)
        {
            request.Content.Position = 0;
        }
        var scan = await _malwareScanner.ScanAsync(request.Content, request.FileName);
        if (!scan.IsClean)
        {
            throw new InvalidOperationException(
                scan.Threat is null ? "The file failed the malware scan." : $"The file failed the malware scan: {scan.Threat}");
        }
        if (request.Content.CanSeek)
        {
            request.Content.Position = 0;
        }

        // Generate a unique GUID-based path and write the file BEFORE inserting the row.
        var filePath = await _fileStorage.UploadAsync(
            request.Content, request.FileName, request.ContentType, requestingUserId, request.ProjectId);

        var document = new Document
        {
            Title = request.Title.Trim(),
            Description = request.Description,
            Category = request.Category,
            Tags = request.Tags,
            FileName = request.FileName,
            FileType = request.ContentType,
            FileSizeBytes = request.FileSizeBytes,
            FilePath = filePath,
            UploadedByUserId = requestingUserId,
            ProjectId = request.ProjectId,
            TaskId = request.TaskId,
            UploadedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };

        try
        {
            _context.Documents.Add(document);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Roll back the stored file so no orphan remains if the metadata insert fails.
            _logger.LogError(ex, "Failed to persist document metadata; rolling back stored file {FilePath}.", filePath);
            await _fileStorage.DeleteAsync(filePath);
            throw;
        }

        await LogActivityAsync(document.DocumentId, "Upload", requestingUserId, document.FileName);

        return document;
    }

    public async Task<IReadOnlyList<Document>> GetMyDocumentsAsync(int requestingUserId, DocumentListFilter filter)
    {
        var query = _context.Documents
            .Include(d => d.Project)
            .Where(d => d.UploadedByUserId == requestingUserId);

        // Filtering
        if (!string.IsNullOrWhiteSpace(filter.Category))
        {
            query = query.Where(d => d.Category == filter.Category);
        }
        if (filter.ProjectId.HasValue)
        {
            query = query.Where(d => d.ProjectId == filter.ProjectId.Value);
        }
        if (filter.FromDate.HasValue)
        {
            query = query.Where(d => d.UploadedDate >= filter.FromDate.Value);
        }
        if (filter.ToDate.HasValue)
        {
            // Inclusive of the whole "to" day.
            var toExclusive = filter.ToDate.Value.Date.AddDays(1);
            query = query.Where(d => d.UploadedDate < toExclusive);
        }

        // Sorting
        query = (filter.SortBy?.ToLowerInvariant()) switch
        {
            "title" => filter.SortDescending ? query.OrderByDescending(d => d.Title) : query.OrderBy(d => d.Title),
            "category" => filter.SortDescending ? query.OrderByDescending(d => d.Category) : query.OrderBy(d => d.Category),
            "filesize" => filter.SortDescending ? query.OrderByDescending(d => d.FileSizeBytes) : query.OrderBy(d => d.FileSizeBytes),
            "uploaddate" => filter.SortDescending ? query.OrderByDescending(d => d.UploadedDate) : query.OrderBy(d => d.UploadedDate),
            _ => query.OrderByDescending(d => d.UploadedDate)
        };

        return await query.ToListAsync();
    }

    public async Task<IReadOnlyList<Document>> GetProjectDocumentsAsync(int projectId, int requestingUserId)
    {
        var user = await _context.Users.FindAsync(requestingUserId);
        if (user is null)
        {
            return Array.Empty<Document>();
        }

        // Project members, the project manager, and administrators may view project documents.
        var authorized = user.Role == UserRole.Administrator
            || await IsProjectMemberOrManagerAsync(projectId, requestingUserId);
        if (!authorized)
        {
            return Array.Empty<Document>();
        }

        return await _context.Documents
            .Include(d => d.Project)
            .Include(d => d.UploadedByUser)
            .Where(d => d.ProjectId == projectId)
            .OrderByDescending(d => d.UploadedDate)
            .ToListAsync();
    }

    public Task<IReadOnlyList<Document>> GetSharedWithMeAsync(int requestingUserId)
        => throw new NotImplementedException("Implemented in User Story 4.");

    public async Task<IReadOnlyList<Document>> SearchAsync(string query, int requestingUserId)
    {
        IQueryable<Document> accessible = (await GetAccessibleDocumentsAsync(requestingUserId))
            .Include(d => d.Project)
            .Include(d => d.UploadedByUser);

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = $"%{query.Trim()}%";
            accessible = accessible.Where(d =>
                EF.Functions.Like(d.Title, term)
                || (d.Description != null && EF.Functions.Like(d.Description, term))
                || (d.Tags != null && EF.Functions.Like(d.Tags, term))
                || EF.Functions.Like(d.UploadedByUser.DisplayName, term)
                || (d.Project != null && EF.Functions.Like(d.Project.Name, term)));
        }

        return await accessible
            .OrderByDescending(d => d.UploadedDate)
            .ToListAsync();
    }

    public Task<(Document Document, Stream Content)?> OpenForDownloadAsync(int documentId, int requestingUserId)
        => throw new NotImplementedException("Implemented in User Story 3.");

    public Task<bool> UpdateMetadataAsync(DocumentMetadataUpdate update, int requestingUserId)
        => throw new NotImplementedException("Implemented in User Story 3.");

    public Task<bool> ReplaceFileAsync(int documentId, string fileName, string contentType,
                                       long fileSizeBytes, Stream content, int requestingUserId)
        => throw new NotImplementedException("Implemented in User Story 3.");

    public Task<bool> DeleteAsync(int documentId, int requestingUserId)
        => throw new NotImplementedException("Implemented in User Story 3.");

    public Task<bool> ShareAsync(int documentId, int? withUserId, string? withTeam, int requestingUserId)
        => throw new NotImplementedException("Implemented in User Story 4.");

    public Task<IReadOnlyList<Document>> GetRecentAsync(int requestingUserId, int count = 5)
        => throw new NotImplementedException("Implemented in User Story 5.");

    public Task<int> GetMyDocumentCountAsync(int requestingUserId)
        => throw new NotImplementedException("Implemented in User Story 5.");

    public Task<DocumentReport?> GetActivityReportAsync(int requestingUserId)
        => throw new NotImplementedException("Implemented in Polish phase.");
}
