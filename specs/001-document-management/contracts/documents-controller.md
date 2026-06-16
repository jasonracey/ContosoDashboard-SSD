# Contract: DocumentsController (HTTP endpoints)

Authorized MVC controller that streams files stored outside `wwwroot`. Registered via
`MapControllers()`. Every action delegates authorization to `IDocumentService`; no bytes
are returned unless the requesting user may access the document (FR-009, FR-010, FR-019,
FR-020; Constitution IV). All routes require an authenticated user (`[Authorize]`).

## Endpoints

### GET `/documents/{id}/download`

- **Purpose**: Download the original file intact (FR-019).
- **Auth**: Authenticated; `IDocumentService.OpenForDownloadAsync(id, currentUserId)` must
  return non-null.
- **Responses**:
  - `200 OK` — `FileStreamResult` with `Content-Disposition: attachment; filename="{original}"`
    and the stored `FileType` content type. Logs a `Download` activity entry.
  - `401 Unauthorized` — no authenticated user.
  - `404 Not Found` — document does not exist OR user is not authorized (existence not
    leaked, SC-010).

### GET `/documents/{id}/preview`

- **Purpose**: In-browser preview for PDF and image types (FR-020).
- **Auth**: Same as download.
- **Responses**:
  - `200 OK` — `FileStreamResult` with `Content-Disposition: inline` for PDF/JPEG/PNG.
  - `415 Unsupported Media Type` (or redirect to download) — for types that cannot be
    previewed (e.g., Office documents) per the edge case in the spec.
  - `401 Unauthorized` / `404 Not Found` — as above.

## Cross-cutting rules

| Rule | Requirement |
|------|-------------|
| No direct file access | Files live outside `wwwroot`; only these endpoints serve them. |
| Authorization in service | The controller never queries the DB or filesystem directly; it calls `IDocumentService`. |
| Streaming | Responses stream from `IFileStorageService.DownloadAsync` (no full-file buffering required). |
| Content type | Uses the document's stored `FileType`; never trusts a client-supplied type. |
| Audit | Successful downloads/previews record a `DocumentActivityLog` entry (FR-032). |

## Notes

This is the only new HTTP surface. All other interactions (upload, list, search, edit,
delete, share) occur through Blazor Server components calling `IDocumentService` directly,
consistent with the existing application architecture.
