# Tasks: Document Upload and Management

**Input**: Design documents from `/specs/001-document-management/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Automated tests are NOT included — the spec did not request them and the
constitution makes tests optional. Validation is performed via the manual scenarios in
[quickstart.md](quickstart.md) and the documented manual security checks.

**Organization**: Tasks are grouped by user story so each story can be implemented and
validated as an independent increment.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story the task belongs to (US1–US5)
- All paths are relative to the repository root; the app project is `ContosoDashboard/`

## Path Conventions

Single Blazor Server web project: source under `ContosoDashboard/` (Models, Services,
Data, Pages, Shared, Controllers). File bytes are stored at runtime under `AppData/uploads/`
(outside `wwwroot`).

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project structure and configuration for the feature

- [X] T001 Create the `ContosoDashboard/Services/Storage/` and `ContosoDashboard/Controllers/` folders for the new storage abstractions and the file-serving controller
- [X] T002 [P] Add a `DocumentStorage` configuration section (RootPath = `AppData/uploads`, MaxFileSizeBytes = 26214400, AllowedExtensions, AllowedContentTypes) to `ContosoDashboard/appsettings.json` and `ContosoDashboard/appsettings.Development.json`
- [X] T003 [P] Add `AppData/uploads/` to `.gitignore` at the repository root so uploaded files are not committed

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Entities, storage/scan abstractions, persistence, and the service skeleton that ALL user stories depend on

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [ ] T004 [P] Create the `Document` entity (int `DocumentId`, Title, Description, Category text, Tags, FileName, FileType[255], FileSizeBytes, FilePath, UploadedByUserId, ProjectId?, TaskId?, UploadedDate, UpdatedDate) per data-model.md in `ContosoDashboard/Models/Document.cs`
- [ ] T005 [P] Create the `DocumentShare` entity (DocumentShareId, DocumentId, SharedWithUserId?, SharedWithTeam?, SharedByUserId, SharedDate) per data-model.md in `ContosoDashboard/Models/DocumentShare.cs`
- [ ] T006 [P] Create the `DocumentActivityLog` entity (DocumentActivityLogId, DocumentId?, Action, PerformedByUserId, Timestamp, Details?) per data-model.md in `ContosoDashboard/Models/DocumentActivityLog.cs`
- [ ] T007 Add navigation collections (`Documents`) to `ContosoDashboard/Models/User.cs`, `ContosoDashboard/Models/Project.cs`, and `ContosoDashboard/Models/TaskItem.cs` (depends on T004)
- [ ] T008 Register `DbSet<Document>`, `DbSet<DocumentShare>`, `DbSet<DocumentActivityLog>` and configure relationships, delete behaviors, and indexes (incl. unique index on `Document.FilePath`) in `OnModelCreating` of `ContosoDashboard/Data/ApplicationDbContext.cs` (depends on T004, T005, T006, T007)
- [ ] T009 [P] Define the `IFileStorageService` interface (`UploadAsync`, `DownloadAsync`, `DeleteAsync`, `GetUrlAsync`) per contracts/IFileStorageService.md in `ContosoDashboard/Services/Storage/IFileStorageService.cs`
- [ ] T010 [P] Define the `IMalwareScanner` interface and `ScanResult` record per contracts/IMalwareScanner.md in `ContosoDashboard/Services/Storage/IMalwareScanner.cs`
- [ ] T011 Implement `LocalFileStorageService` (GUID path `{userId}/{projectId|"personal"}/{guid}.{ext}` under the configured root outside `wwwroot`, ensures root exists, `GetUrlAsync` returns the controller route) in `ContosoDashboard/Services/Storage/LocalFileStorageService.cs` (depends on T009)
- [ ] T012 [P] Implement `StubMalwareScanner` (returns clean for allowed files; documented training stand-in) in `ContosoDashboard/Services/Storage/StubMalwareScanner.cs` (depends on T010)
- [ ] T013 Define the `IDocumentService` interface and DTOs (`DocumentUploadRequest`, `DocumentListFilter`, `DocumentMetadataUpdate`, `DocumentReport`) per contracts/IDocumentService.md in `ContosoDashboard/Services/IDocumentService.cs`
- [ ] T014 Create the `DocumentService` skeleton with shared helpers — the fixed Category list, extension/size validation, the permission-scoped accessible-document query (own ∪ project ∪ shared/team ∪ elevated role), and an activity-log writer — in `ContosoDashboard/Services/DocumentService.cs` (depends on T008, T013)
- [ ] T015 Register `IFileStorageService`, `IMalwareScanner`, and `IDocumentService` in DI, ensure the upload root is created at startup, and add `app.MapControllers()` to the pipeline in `ContosoDashboard/Program.cs` (depends on T011, T012, T014)

**Checkpoint**: Foundation ready — user story implementation can now begin

---

## Phase 3: User Story 1 - Upload and Organize Documents (Priority: P1) 🎯 MVP

**Goal**: Employees can upload supported files with metadata and immediately see them in "My Documents".

**Independent Test**: Upload a supported file (≤25 MB) with title and category; confirm it appears in My Documents with correct metadata, is stored under `AppData/uploads/...`, and that oversized/unsupported files are rejected with no orphaned file or row.

### Implementation for User Story 1

- [ ] T016 [US1] Implement `DocumentService.UploadAsync` following validate → scan → authorize (project membership for project uploads) → write file via `IFileStorageService` → insert metadata row → log `Upload`, with rollback (delete file) on persistence failure, in `ContosoDashboard/Services/DocumentService.cs`
- [ ] T017 [US1] Implement `DocumentService.GetMyDocumentsAsync` returning the requesting user's own uploads with title, category, upload date, file size, and associated project, in `ContosoDashboard/Services/DocumentService.cs`
- [ ] T018 [US1] Create the `Documents.razor` page with an upload modal — `InputFile` (with `@key`), multi-file selection, MemoryStream copy pattern, shared category/project/tags, per-file title defaulting to filename, progress indicator, and success/error messages — plus a "My Documents" table, in `ContosoDashboard/Pages/Documents.razor`
- [ ] T019 [US1] Add an `[Authorize]` "Documents" navigation entry pointing to `/documents` in `ContosoDashboard/Shared/NavMenu.razor`

**Checkpoint**: Upload + list works end-to-end — MVP is demonstrable and independently testable

---

## Phase 4: User Story 2 - Browse, Search, and Filter Documents (Priority: P1)

**Goal**: Users can sort and filter their documents and search across everything they may access, permission-scoped.

**Independent Test**: With several documents seeded across categories/projects, confirm sort, filter (category/project/date range), and search by title/description/tag/uploader/project return correct, permission-scoped results within ~2 seconds.

### Implementation for User Story 2

- [ ] T020 [US2] Extend `DocumentService.GetMyDocumentsAsync` to apply `DocumentListFilter` sorting (title, upload date, category, file size) and filtering (category, project, date range) in `ContosoDashboard/Services/DocumentService.cs`
- [ ] T021 [US2] Implement `DocumentService.SearchAsync` over the permission-scoped accessible set, matching title, description, tags, uploader name, and project, in `ContosoDashboard/Services/DocumentService.cs`
- [ ] T022 [US2] Implement `DocumentService.GetProjectDocumentsAsync` (project members may view/download project documents) in `ContosoDashboard/Services/DocumentService.cs`
- [ ] T023 [US2] Add sort controls, filter controls (category/project/date range), and a search box wired to the service in `ContosoDashboard/Pages/Documents.razor`

**Checkpoint**: Users can reliably find documents; US1 and US2 both work independently

---

## Phase 5: User Story 3 - Access, Preview, Edit, and Delete Documents (Priority: P2)

**Goal**: Users can download/preview documents, edit metadata, replace files, and delete their own (Project Managers manage their projects' documents).

**Independent Test**: Download a file intact, preview a PDF/image inline, edit metadata, replace a file, delete with confirmation, and verify a non-owner/non-manager is denied (404).

### Implementation for User Story 3

- [ ] T024 [US3] Implement `DocumentService.OpenForDownloadAsync` returning the document + stream only when authorized (else null), logging a `Download` entry, in `ContosoDashboard/Services/DocumentService.cs`
- [ ] T025 [US3] Create `DocumentsController` with `[Authorize]` `GET /documents/{id}/download` (attachment) and `GET /documents/{id}/preview` (inline for PDF/JPEG/PNG, otherwise fall back to download), delegating authorization to `IDocumentService` and returning 404 on no-access, in `ContosoDashboard/Controllers/DocumentsController.cs` (depends on T024)
- [ ] T026 [US3] Implement `DocumentService.UpdateMetadataAsync` and `ReplaceFileAsync` (authorized to uploader, owning Project Manager, or Administrator; replace writes new GUID file, deletes the old, updates path/type/size/UpdatedDate) in `ContosoDashboard/Services/DocumentService.cs`
- [ ] T027 [US3] Implement `DocumentService.DeleteAsync` (hard delete of row + stored file after authorization, logs `Delete`) in `ContosoDashboard/Services/DocumentService.cs`
- [ ] T028 [US3] Add download/preview links, an edit-metadata form, a file-replace control, and a delete action with confirmation to `ContosoDashboard/Pages/Documents.razor`

**Checkpoint**: Full document lifecycle works; US1–US3 independently functional

---

## Phase 6: User Story 4 - Share Documents with Users and Teams (Priority: P2)

**Goal**: Owners share documents with users or teams; recipients are notified and see them under "Shared with Me"; non-recipients cannot access.

**Independent Test**: Share with a user and a team, confirm in-app notification and "Shared with Me" visibility, confirm non-recipients are excluded, and that moving users in/out of the team grants/revokes access dynamically.

### Implementation for User Story 4

- [ ] T029 [US4] Implement `DocumentService.ShareAsync` (owner/admin only; creates a `DocumentShare` for a user or team; logs `Share`) and `GetSharedWithMeAsync` (direct shares plus dynamic team/department matches) in `ContosoDashboard/Services/DocumentService.cs`
- [ ] T030 [US4] On share, create in-app notifications for recipients via the existing `INotificationService` within `DocumentService.ShareAsync` in `ContosoDashboard/Services/DocumentService.cs` (depends on T029)
- [ ] T031 [US4] Create the `SharedWithMe.razor` page listing documents shared with the current user and add an `[Authorize]` nav entry in `ContosoDashboard/Pages/SharedWithMe.razor` and `ContosoDashboard/Shared/NavMenu.razor`
- [ ] T032 [US4] Add a Share action with a user/team picker to `ContosoDashboard/Pages/Documents.razor`

**Checkpoint**: Sharing works end-to-end; US1–US4 independently functional

---

## Phase 7: User Story 5 - Integration with Tasks, Dashboard, and Notifications (Priority: P3)

**Goal**: Documents connect to tasks, the dashboard, and project notifications.

**Independent Test**: Upload a document from a task (auto-associates with the task's project), view the Recent Documents widget and count on the dashboard, and confirm project members are notified when a document is added to their project.

### Implementation for User Story 5

- [ ] T033 [US5] Implement `DocumentService.GetRecentAsync` (last 5 of the user's uploads) and `GetMyDocumentCountAsync` in `ContosoDashboard/Services/DocumentService.cs`
- [ ] T034 [US5] Add a "Recent Documents" widget and a document-count summary card to `ContosoDashboard/Pages/Index.razor` (extend `DashboardService` in `ContosoDashboard/Services/DashboardService.cs` if an aggregate is needed)
- [ ] T035 [US5] Add document view/attach/upload to the task detail experience and ensure documents uploaded from a task set `TaskId` and inherit the task's `ProjectId` (UploadAsync) in `ContosoDashboard/Pages/Tasks.razor`
- [ ] T036 [US5] Add a project documents tab/section (view + download, PM upload) to `ContosoDashboard/Pages/ProjectDetails.razor`
- [ ] T037 [US5] Notify project members when a new document is added to their project, via `INotificationService` in `DocumentService.UploadAsync`, in `ContosoDashboard/Services/DocumentService.cs`

**Checkpoint**: All five user stories independently functional and integrated

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Audit reporting, performance, security verification, and final validation

- [ ] T038 [P] Implement `DocumentService.GetActivityReportAsync` (most-uploaded types, most active uploaders, access patterns; Administrator-only) and an admin report view, in `ContosoDashboard/Services/DocumentService.cs` and `ContosoDashboard/Pages/Documents.razor`
- [ ] T039 [P] Verify list performance for up to 500 documents (confirm indexes from T008; add pagination/`Take` if needed) in `ContosoDashboard/Services/DocumentService.cs`
- [ ] T040 Security verification pass: `[Authorize]` on pages and controller, IDOR 404 semantics, GUID-only `FilePath`, extension/MIME whitelist, files outside `wwwroot`, content type from stored value — across `ContosoDashboard/Services/DocumentService.cs`, `ContosoDashboard/Controllers/DocumentsController.cs`, and `ContosoDashboard/Pages/Documents.razor`
- [ ] T041 Execute all validation scenarios in `specs/001-document-management/quickstart.md` and confirm the success criteria (SC-005..SC-010) are met

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories
- **User Stories (Phase 3–7)**: All depend on Foundational completion; then can proceed in priority order (P1 → P1 → P2 → P2 → P3) or in parallel by different developers
- **Polish (Phase 8)**: Depends on the targeted user stories being complete

### User Story Dependencies

- **US1 (P1)**: Only Foundational — no dependency on other stories
- **US2 (P1)**: Foundational; reuses US1's page and `GetMyDocuments` but is independently testable
- **US3 (P2)**: Foundational; adds lifecycle to existing documents — independently testable
- **US4 (P2)**: Foundational; independently testable
- **US5 (P3)**: Foundational; integrates with US1 upload — independently testable

### Within Each User Story

- Service methods before the UI that consumes them
- Controller (T025) after its service method (T024)
- Notification wiring (T030) after the share method (T029)

### Parallel Opportunities

- Setup: T002 and T003 in parallel
- Foundational: T004/T005/T006 in parallel; T009/T010 in parallel; T012 alongside T011
- All shared `DocumentService.cs` edits within a story are sequential (same file)
- Different user stories can be staffed in parallel once Foundational is done
- Polish: T038 and T039 in parallel

---

## Parallel Example: Foundational Phase

```bash
# Entities (different files) together:
Task: "Create Document model in ContosoDashboard/Models/Document.cs"
Task: "Create DocumentShare model in ContosoDashboard/Models/DocumentShare.cs"
Task: "Create DocumentActivityLog model in ContosoDashboard/Models/DocumentActivityLog.cs"

# Abstractions (different files) together:
Task: "Define IFileStorageService in ContosoDashboard/Services/Storage/IFileStorageService.cs"
Task: "Define IMalwareScanner in ContosoDashboard/Services/Storage/IMalwareScanner.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 only)

1. Complete Phase 1 (Setup) and Phase 2 (Foundational) — the critical blocking work
2. Complete Phase 3 (US1: upload + list)
3. **STOP and VALIDATE** using quickstart Scenarios 1–3
4. Demo the MVP

### Incremental Delivery

1. Setup + Foundational → foundation ready
2. US1 → validate → demo (MVP: upload & organize)
3. US2 → validate → demo (findability)
4. US3 → validate → demo (lifecycle)
5. US4 → validate → demo (sharing)
6. US5 → validate → demo (integrations)
7. Polish (audit reporting, performance, security, quickstart)

### Parallel Team Strategy

Once Foundational is complete, US1/US2 (page-heavy), US3 (controller + lifecycle), US4
(sharing), and US5 (integrations) can be split across developers; coordinate edits to the
shared `DocumentService.cs`.

---

## Notes

- No automated test tasks are included (not requested); validate via `quickstart.md`.
- `[P]` = different files, no dependencies. Tasks editing the same file (notably
  `DocumentService.cs` and `Documents.razor`) are intentionally sequential.
- Security requirements are enforced inside `DocumentService` (single authorization
  boundary) and verified in T040.
- Commit after each task or logical group; stop at any checkpoint to validate a story.
