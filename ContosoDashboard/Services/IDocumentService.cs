using ContosoDashboard.Models;

namespace ContosoDashboard.Services;

/// <summary>The fixed, predefined document categories presented in the UI and validated on write.</summary>
public static class DocumentCategories
{
    public static readonly IReadOnlyList<string> All = new[]
    {
        "Project Documents",
        "Team Resources",
        "Personal Files",
        "Reports",
        "Presentations",
        "Other"
    };
}

/// <summary>Request to upload a single document. The caller copies the file into a seekable stream.</summary>
public sealed record DocumentUploadRequest(
    string Title,
    string? Description,
    string Category,
    string? Tags,
    int? ProjectId,
    int? TaskId,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    Stream Content);

/// <summary>Sort/filter options for the "My Documents" list.</summary>
public sealed record DocumentListFilter(
    string? Category = null,
    int? ProjectId = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    string? SortBy = null,            // title | uploadDate | category | fileSize
    bool SortDescending = false);

/// <summary>Metadata edit for an existing document.</summary>
public sealed record DocumentMetadataUpdate(
    int DocumentId,
    string Title,
    string? Description,
    string Category,
    string? Tags);

/// <summary>Aggregated audit/reporting data for administrators.</summary>
public sealed record DocumentReport(
    IReadOnlyList<(string FileType, int Count)> MostUploadedTypes,
    IReadOnlyList<(string UploaderName, int Count)> MostActiveUploaders,
    IReadOnlyList<(string Action, int Count)> AccessPatterns);

/// <summary>
/// Business-logic orchestration for document upload and management. This service is the
/// sole authorization boundary for documents (IDOR protection); pages and controllers MUST
/// go through it and MUST NOT access the database directly.
/// </summary>
public interface IDocumentService
{
    Task<Document> UploadAsync(DocumentUploadRequest request, int requestingUserId);

    Task<IReadOnlyList<Document>> GetMyDocumentsAsync(int requestingUserId, DocumentListFilter filter);

    Task<IReadOnlyList<Document>> GetProjectDocumentsAsync(int projectId, int requestingUserId);

    Task<IReadOnlyList<Document>> GetSharedWithMeAsync(int requestingUserId);

    Task<IReadOnlyList<Document>> SearchAsync(string query, int requestingUserId);

    Task<(Document Document, Stream Content)?> OpenForDownloadAsync(int documentId, int requestingUserId);

    Task<bool> UpdateMetadataAsync(DocumentMetadataUpdate update, int requestingUserId);

    Task<bool> ReplaceFileAsync(int documentId, string fileName, string contentType,
                                long fileSizeBytes, Stream content, int requestingUserId);

    Task<bool> DeleteAsync(int documentId, int requestingUserId);

    Task<bool> ShareAsync(int documentId, int? withUserId, string? withTeam, int requestingUserId);

    Task<IReadOnlyList<Document>> GetRecentAsync(int requestingUserId, int count = 5);

    Task<int> GetMyDocumentCountAsync(int requestingUserId);

    Task<DocumentReport?> GetActivityReportAsync(int requestingUserId);
}
