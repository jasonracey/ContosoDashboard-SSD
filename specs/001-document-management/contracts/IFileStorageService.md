# Contract: IFileStorageService

Storage abstraction that isolates file persistence so the local filesystem implementation
can be swapped for Azure Blob Storage with no changes to business logic, UI, or schema
(Constitution III; FR-034, FR-035).

## Interface

```csharp
namespace ContosoDashboard.Services.Storage;

public interface IFileStorageService
{
    // Saves the stream and returns the relative storage path/identity that is persisted on the Document.
    // The implementation generates a GUID-based unique path: {userId}/{projectId|"personal"}/{guid}.{ext}
    Task<string> UploadAsync(Stream content, string originalFileName, string contentType,
                             int userId, int? projectId, CancellationToken ct = default);

    // Opens a readable stream for the stored file. Throws/returns null if the path is unknown.
    Task<Stream> DownloadAsync(string filePath, CancellationToken ct = default);

    // Permanently removes the stored file. Idempotent: succeeds even if already absent.
    Task DeleteAsync(string filePath, CancellationToken ct = default);

    // Returns a URL/locator for the file. Local implementation returns the authorized
    // controller route; Azure implementation will return a time-limited SAS URL.
    Task<string> GetUrlAsync(string filePath, TimeSpan expiration, CancellationToken ct = default);
}
```

## Behavioral contract

| Rule | Requirement |
|------|-------------|
| Unique path | `UploadAsync` MUST produce a unique GUID-based path before any DB insert (FR-011, FR-013). |
| No user filenames in path | The stored path MUST NOT incorporate the user-supplied filename (FR-012). |
| Outside `wwwroot` | The local implementation MUST write under the configured root outside `wwwroot` (Constitution IV). |
| Portable path | Returned path MUST be relative and portable (valid as both a local subpath and an Azure blob name). |
| Failure isolation | If `UploadAsync` throws, no file is left behind that the caller would treat as stored. |
| Idempotent delete | `DeleteAsync` MUST NOT throw if the file is already gone. |

## Implementations

- **`LocalFileStorageService`** (this release): uses `System.IO`; root from
  `DocumentStorage:RootPath` config (default `AppData/uploads`); ensures the root exists at
  startup; `GetUrlAsync` returns the `DocumentsController` download route.
- **`AzureBlobStorageService`** (future, not in scope): uses `Azure.Storage.Blobs`; same
  path as blob name; `GetUrlAsync` returns a SAS URL. Swapped via DI only.
