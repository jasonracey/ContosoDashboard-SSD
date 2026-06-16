using Microsoft.Extensions.Options;

namespace ContosoDashboard.Services.Storage;

/// <summary>
/// Local filesystem implementation of <see cref="IFileStorageService"/>. Stores files
/// outside wwwroot using GUID-based paths generated before any database insert, preventing
/// path-traversal attacks and orphaned/duplicate records.
///
/// PRODUCTION NOTE: A future AzureBlobStorageService can implement the same interface; the
/// returned relative path doubles as an Azure blob name. Swap via DI — no other code changes.
/// </summary>
public sealed class LocalFileStorageService : IFileStorageService
{
    private readonly string _rootAbsolutePath;

    public LocalFileStorageService(IOptions<DocumentStorageOptions> options, IHostEnvironment environment)
    {
        var configuredRoot = options.Value.RootPath;
        _rootAbsolutePath = Path.IsPathRooted(configuredRoot)
            ? configuredRoot
            : Path.GetFullPath(Path.Combine(environment.ContentRootPath, configuredRoot));

        Directory.CreateDirectory(_rootAbsolutePath);
    }

    public async Task<string> UploadAsync(Stream content, string originalFileName, string contentType,
                                          int userId, int? projectId, CancellationToken ct = default)
    {
        // Never use the user-supplied filename in the path; only its (validated) extension.
        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        var scope = projectId.HasValue ? projectId.Value.ToString() : "personal";
        var uniqueName = $"{Guid.NewGuid():N}{extension}";

        // Relative, portable path (forward slashes) — valid as a local subpath and an Azure blob name.
        var relativePath = $"{userId}/{scope}/{uniqueName}";
        var absolutePath = ResolveAbsolutePath(relativePath);

        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);

        await using (var fileStream = new FileStream(absolutePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await content.CopyToAsync(fileStream, ct);
        }

        return relativePath;
    }

    public Task<Stream> DownloadAsync(string filePath, CancellationToken ct = default)
    {
        var absolutePath = ResolveAbsolutePath(filePath);
        Stream stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string filePath, CancellationToken ct = default)
    {
        var absolutePath = ResolveAbsolutePath(filePath);
        if (File.Exists(absolutePath))
        {
            File.Delete(absolutePath);
        }
        return Task.CompletedTask;
    }

    public Task<string> GetUrlAsync(string filePath, TimeSpan expiration, CancellationToken ct = default)
    {
        // Local storage has no public URL; return the relative locator. The UI serves files
        // via the authorized DocumentsController by document id.
        return Task.FromResult(filePath);
    }

    /// <summary>
    /// Resolves a stored relative path to an absolute path and guards against path traversal
    /// outside the configured storage root.
    /// </summary>
    private string ResolveAbsolutePath(string relativePath)
    {
        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(_rootAbsolutePath, normalized));

        var rootWithSeparator = _rootAbsolutePath.EndsWith(Path.DirectorySeparatorChar)
            ? _rootAbsolutePath
            : _rootAbsolutePath + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Resolved file path is outside the storage root.");
        }

        return fullPath;
    }
}
