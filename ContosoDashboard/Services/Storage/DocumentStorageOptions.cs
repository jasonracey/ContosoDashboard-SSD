namespace ContosoDashboard.Services.Storage;

/// <summary>
/// Bound from the "DocumentStorage" configuration section. Shared by the storage service
/// (root path) and the document service (validation limits).
/// </summary>
public sealed class DocumentStorageOptions
{
    public const string SectionName = "DocumentStorage";

    /// <summary>Root directory for uploaded files, relative to the content root (outside wwwroot).</summary>
    public string RootPath { get; set; } = "AppData/uploads";

    /// <summary>Maximum allowed size per file in bytes (default 25 MB).</summary>
    public long MaxFileSizeBytes { get; set; } = 26214400;

    /// <summary>Whitelisted file extensions (lower-case, with leading dot).</summary>
    public List<string> AllowedExtensions { get; set; } = new();

    /// <summary>Whitelisted MIME content types.</summary>
    public List<string> AllowedContentTypes { get; set; } = new();
}
