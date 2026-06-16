# Contract: IDocumentService

Business-logic orchestration for the feature. Lives in the Services layer; it is the sole
authorization boundary for documents (IDOR protection, Constitution IV/V). Pages and the
controller MUST go through it and MUST NOT reach `ApplicationDbContext` directly.

## DTOs (representative)

```csharp
public sealed record DocumentUploadRequest(
    string Title, string? Description, string Category, string? Tags,
    int? ProjectId, int? TaskId,
    string FileName, string ContentType, long FileSizeBytes, Stream Content);

public sealed record DocumentListFilter(
    string? Category, int? ProjectId, DateTime? FromDate, DateTime? ToDate,
    string? SortBy /* title|uploadDate|category|fileSize */, bool SortDescending);

public sealed record DocumentMetadataUpdate(
    int DocumentId, string Title, string? Description, string Category, string? Tags);
```

## Interface

```csharp
namespace ContosoDashboard.Services;

public interface IDocumentService
{
    // FR-001..FR-013: validate -> scan -> authorize -> store -> persist -> notify. Returns created document.
    Task<Document> UploadAsync(DocumentUploadRequest request, int requestingUserId);

    // FR-014..FR-016: the requesting user's own uploads, with sort/filter applied.
    Task<IReadOnlyList<Document>> GetMyDocumentsAsync(int requestingUserId, DocumentListFilter filter);

    // FR-017: documents for a project the requesting user may view.
    Task<IReadOnlyList<Document>> GetProjectDocumentsAsync(int projectId, int requestingUserId);

    // FR-026: documents shared with the requesting user (direct or via their team).
    Task<IReadOnlyList<Document>> GetSharedWithMeAsync(int requestingUserId);

    // FR-018 (clarified): search across all documents the user may access.
    Task<IReadOnlyList<Document>> SearchAsync(string query, int requestingUserId);

    // FR-010/FR-019/FR-020: returns document + opened stream IF authorized, else null.
    Task<(Document Document, Stream Content)?> OpenForDownloadAsync(int documentId, int requestingUserId);

    // FR-021: edit metadata (uploader only, or project manager for project docs).
    Task<bool> UpdateMetadataAsync(DocumentMetadataUpdate update, int requestingUserId);

    // FR-022: replace the file with a new version (authorized editors only).
    Task<bool> ReplaceFileAsync(int documentId, string fileName, string contentType,
                                long fileSizeBytes, Stream content, int requestingUserId);

    // FR-023/FR-024: hard delete after confirmation (uploader or project manager).
    Task<bool> DeleteAsync(int documentId, int requestingUserId);

    // FR-025/FR-026/FR-027: share with a user or a team; notifies recipients.
    Task<bool> ShareAsync(int documentId, int? withUserId, string? withTeam, int requestingUserId);

    // FR-029: last N documents uploaded by the user (dashboard widget) and total count.
    Task<IReadOnlyList<Document>> GetRecentAsync(int requestingUserId, int count = 5);
    Task<int> GetMyDocumentCountAsync(int requestingUserId);

    // FR-033: admin reporting aggregates (most types, most active uploaders, access patterns).
    Task<DocumentReport> GetActivityReportAsync(int requestingUserId);
}
```

## Authorization rules (enforced inside the service)

| Operation | Allowed when |
|-----------|--------------|
| Upload to project | Requesting user is a member/manager of the target project (FR per US1 scenario 4); personal upload always allowed. |
| View / download / preview | Document is in the user's accessible set (own ∪ project ∪ shared ∪ elevated) — see data-model.md (FR-010, FR-019, FR-020, FR-031). |
| Search / list | Results restricted to the accessible set (FR-018 clarified). |
| Edit metadata / replace file | Uploader, or Project Manager of the document's project, or Administrator (FR-021, FR-022, FR-024). |
| Delete | Uploader, or Project Manager of the document's project, or Administrator (FR-023, FR-024). |
| Share | Document owner (uploader), or Administrator (FR-025). |

## Cross-cutting behavior

- **Validation** (FR-002, FR-003): reject disallowed extensions/MIME types and files
  > 25 MB with specific error messages before scanning.
- **Scan** (FR-008): call `IMalwareScanner`; reject and persist nothing if not clean.
- **Atomicity** (FR-013): write file via `IFileStorageService` then insert row; on row
  failure, delete the file; never leave orphans.
- **Notifications** (FR-026, FR-030): on share, notify recipients; on new project
  document, notify project members — via existing `INotificationService`.
- **Audit** (FR-032): write a `DocumentActivityLog` entry for upload, download, delete,
  and share.
- **Unauthorized access** returns `null`/`false` (not found semantics) rather than leaking
  existence (FR-010, SC-010).
