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

    /// <summary>The fixed, predefined document categories presented in the UI and validated on write.</summary>
    public static readonly IReadOnlyList<string> Categories = new[]
    {
        "Project Documents",
        "Team Resources",
        "Personal Files",
        "Reports",
        "Presentations",
        "Other"
    };

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
    protected static bool IsValidCategory(string category) => Categories.Contains(category);

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

    // ---- Story operations (implemented in their feature phases) -------------------------

    public Task<Document> UploadAsync(DocumentUploadRequest request, int requestingUserId)
        => throw new NotImplementedException("Implemented in User Story 1.");

    public Task<IReadOnlyList<Document>> GetMyDocumentsAsync(int requestingUserId, DocumentListFilter filter)
        => throw new NotImplementedException("Implemented in User Story 1/2.");

    public Task<IReadOnlyList<Document>> GetProjectDocumentsAsync(int projectId, int requestingUserId)
        => throw new NotImplementedException("Implemented in User Story 2.");

    public Task<IReadOnlyList<Document>> GetSharedWithMeAsync(int requestingUserId)
        => throw new NotImplementedException("Implemented in User Story 4.");

    public Task<IReadOnlyList<Document>> SearchAsync(string query, int requestingUserId)
        => throw new NotImplementedException("Implemented in User Story 2.");

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
