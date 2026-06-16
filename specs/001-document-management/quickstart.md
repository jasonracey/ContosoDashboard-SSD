# Quickstart: Document Upload and Management

A validation guide that proves the feature works end-to-end in the offline training
environment. It references [data-model.md](data-model.md) and
[contracts/](contracts/) instead of restating implementation details. Implementation
itself is produced by `/speckit.tasks` and the implementation phase.

## Prerequisites

- .NET 8 SDK installed.
- SQL Server LocalDB available (default `DefaultConnection` in `appsettings.json`).
- No internet connection required (offline-first).

## One-time clean database (only if prior upload attempts left orphaned rows)

```powershell
sqllocaldb stop mssqllocaldb
sqllocaldb delete mssqllocaldb
# Database is recreated and seeded automatically on next run.
```

## Run the application

```powershell
cd ContosoDashboard
dotnet run
```

On first run the database is created/seeded and the upload root
(`DocumentStorage:RootPath`, default `AppData/uploads`, outside `wwwroot`) is created
automatically. Browse to the app and sign in with a seeded user (e.g.
`camille.nicole@contoso.com`, Project Manager).

## Validation scenarios

Each scenario maps to user stories and functional requirements in [spec.md](spec.md).

### Scenario 1 — Upload and list (US1; FR-001..FR-007)

1. Open **Documents**, click **Upload**, choose a PDF (≤25 MB), set a title and a category,
   submit.
2. **Expect**: progress indicator, then success message; the document appears in
   **My Documents** with title, category, upload date, file size, and uploader.
3. Confirm a file exists under `AppData/uploads/{userId}/personal/{guid}.pdf` and the DB
   `Documents` row's `FilePath` matches.

### Scenario 2 — Validation & scan rejection (FR-002, FR-003, FR-008)

1. Attempt to upload a `.exe` (or a 30 MB file).
2. **Expect**: a clear, specific rejection message; **no** file under `AppData/uploads`
   and **no** new `Documents` row (FR-013 — no orphans).

### Scenario 3 — Multi-file upload (US1; FR-001/FR-005 clarified)

1. Select three supported files at once; set one category, project, and tags; submit.
2. **Expect**: three documents created, each titled by its filename, all sharing the chosen
   category/project/tags.

### Scenario 4 — Browse, sort, filter, search (US2; FR-014..FR-018)

1. In **My Documents**, sort by upload date and by file size; filter by category and date
   range.
2. Run a search by title, tag, and uploader name.
3. **Expect**: correct results within ~2 seconds; only documents the user may access appear
   (permission-scoped search).

### Scenario 5 — Download & preview (US3; FR-019, FR-020)

1. Download a document → original file returned intact via `/documents/{id}/download`.
2. Preview a PDF/image → displays inline via `/documents/{id}/preview` within ~3 seconds.
3. Preview an Office file → offered a download instead (edge case).

### Scenario 6 — Edit, replace, delete (US3; FR-021..FR-024)

1. Edit a document's title/category/tags → changes reflected in lists and search.
2. Replace the file with a newer version → subsequent download returns the updated file;
   the old stored file is removed.
3. Delete a document and confirm → row and stored file are permanently removed.

### Scenario 7 — Sharing (US4; FR-025..FR-027)

1. As an owner, share a document with another user and with a team (department).
2. **Expect**: recipient(s) get an in-app notification and see it under **Shared with Me**;
   a non-recipient cannot find or open it.
3. Move a user into the shared team → they gain access; remove them → access is lost
   (dynamic membership).

### Scenario 8 — Authorization / IDOR (FR-009, FR-010; SC-010)

1. As a user without access, request `/documents/{id}/download` for someone else's private
   document by guessing the id.
2. **Expect**: `404 Not Found` (existence not leaked); access denied.

### Scenario 9 — Integrations (US5; FR-028..FR-030)

1. From a task detail page, upload/attach a document → it associates with the task and the
   task's project.
2. Add a document to a project → project members receive a notification.
3. On the dashboard, confirm the **Recent Documents** widget (last 5) and the document
   count card.

### Scenario 10 — Offline operation (FR-034)

1. Disconnect from the network and repeat Scenarios 1, 4, and 5.
2. **Expect**: all core functionality continues to work.

## Manual security checks (constitution-aligned)

- Unauthenticated access to **Documents** and `/documents/...` routes redirects to login
  (`[Authorize]`).
- A non-owner, non-manager cannot edit/delete another user's document.
- Stored filenames are GUID-based; no user-supplied filename appears in `FilePath`.

## Success signals

All scenarios pass, and the measurable outcomes in spec.md (SC-005 ≤3 clicks, SC-006 ≤30 s
upload, SC-007 ≤2 s list for 500 docs, SC-008 ≤2 s search, SC-009 ≤3 s preview, SC-010
100% unauthorized attempts denied) are observed.
