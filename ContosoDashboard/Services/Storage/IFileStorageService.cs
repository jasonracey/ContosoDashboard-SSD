namespace ContosoDashboard.Services.Storage;

/// <summary>
/// Storage abstraction that isolates file persistence so the local filesystem
/// implementation can be swapped for a cloud implementation (e.g. Azure Blob Storage)
/// without changing business logic, UI, or database schema.
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Saves the stream and returns the relative, portable storage path persisted on the
    /// Document. The implementation generates a unique GUID-based path of the form
    /// {userId}/{projectId|"personal"}/{guid}.{ext} before any database insert.
    /// </summary>
    Task<string> UploadAsync(Stream content, string originalFileName, string contentType,
                             int userId, int? projectId, CancellationToken ct = default);

    /// <summary>Opens a readable stream for the stored file.</summary>
    Task<Stream> DownloadAsync(string filePath, CancellationToken ct = default);

    /// <summary>Permanently removes the stored file. Idempotent: succeeds if already absent.</summary>
    Task DeleteAsync(string filePath, CancellationToken ct = default);

    /// <summary>
    /// Returns a URL/locator for the file. The local implementation returns the authorized
    /// controller route; a future Azure implementation would return a time-limited SAS URL.
    /// </summary>
    Task<string> GetUrlAsync(string filePath, TimeSpan expiration, CancellationToken ct = default);
}
