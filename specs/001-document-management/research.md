# Phase 0 Research: Document Upload and Management

All Technical Context items were resolved from the existing codebase, the feature spec
(including its 5 clarifications), and the project constitution. No `NEEDS CLARIFICATION`
markers remained. The decisions below record the choices that shape Phase 1 design.

## 1. File storage location & layout

- **Decision**: Store file bytes on the local filesystem outside `wwwroot`, under a
  configurable root (`AppData/uploads`), using the path pattern
  `{userId}/{projectId|"personal"}/{guid}.{ext}`. Persist the relative path in the DB.
- **Rationale**: Constitution IV requires files outside `wwwroot` with GUID-based names
  generated before DB insert to prevent IDOR/path traversal and orphan records. The
  relative, portable path doubles as a future Azure Blob name (Constitution III).
- **Alternatives considered**: Storing bytes in the database (rejected — bloats LocalDB,
  poor streaming, no clean Azure migration); storing under `wwwroot` (rejected — bypasses
  authorization, security violation).

## 2. Serving downloads & previews

- **Decision**: Serve files through a new authorized MVC controller
  (`DocumentsController`) with `download` and `preview` actions; register via
  `MapControllers()`. Preview returns inline `Content-Disposition` for PDF/images;
  other types fall back to download.
- **Rationale**: Files live outside `wwwroot`, so static-file middleware cannot serve
  them. A controller lets `DocumentService` enforce per-document authorization before any
  bytes are returned (FR-009, FR-010, FR-019, FR-020).
- **Alternatives considered**: Blazor streaming via JS interop (rejected — weaker auth
  story, more complexity); copying into `wwwroot` on demand (rejected — security and
  cleanup hazards).

## 3. Malware scanning offline (FR-008, clarified)

- **Decision**: Define `IMalwareScanner` with a `StubMalwareScanner` that returns "clean"
  in the offline build, invoked in the upload pipeline before persistence. Swappable for a
  real scanner in production via DI.
- **Rationale**: Clarification chose a pluggable scan interface with a simulated scanner so
  the FR-008 contract is enforced in the workflow while honoring offline-first
  (Constitution II) and the abstraction lesson (Constitution III).
- **Alternatives considered**: Bundling a real scanner (rejected — external/offline
  conflict, heavy); dropping scanning (rejected — clarification kept the requirement).

## 4. Upload transaction ordering (FR-013)

- **Decision**: Sequence = validate (size/extension) → scan → authorize → generate GUID
  path → write file to disk → create DB record → send notifications. If the DB write
  fails after the file is written, delete the just-written file; if the file write fails,
  no DB record is created.
- **Rationale**: Generating the unique path before insert and writing the file before the
  metadata row prevents both orphaned DB rows (empty/duplicate paths) and orphaned files,
  satisfying FR-011/FR-013 and Constitution IV.
- **Alternatives considered**: DB-first then file (rejected — orphaned rows on file
  failure, the documented failure mode); single DB transaction wrapping disk I/O
  (rejected — filesystem is not transactional with EF).

## 5. Multi-file upload metadata (FR-001/FR-005, clarified)

- **Decision**: Each selected file becomes its own `Document`; title defaults to the
  filename (editable per file) while category, project, and tags chosen once apply to the
  whole batch. Each file is processed through the full pipeline independently.
- **Rationale**: Matches the clarification and keeps uploads within the ≤3-click goal
  (SC-005) without per-file retyping of shared metadata.
- **Alternatives considered**: Full per-file forms (rejected — violates click budget);
  single combined document (rejected — breaks per-file download/preview).

## 6. Team-based sharing semantics (FR-025, clarified)

- **Decision**: A `DocumentShare` row targets either a specific user or a team
  (department). Access is evaluated dynamically against current team membership, so
  members who join later gain access and removed members lose it.
- **Rationale**: Clarification selected dynamic, membership-following access; the app
  already groups users by `Department`, which serves as the "team".
- **Alternatives considered**: Static member snapshot at share time (rejected — stale
  access lists, contradicts clarification).

## 7. Permission model & search scope (FR-018/FR-031, clarified)

- **Decision**: `DocumentService` computes the set of documents a user may access — own
  uploads ∪ documents of projects the user belongs to ∪ documents shared directly or via
  the user's team — plus elevated-role visibility (Team Lead → team members' documents,
  Project Manager → their projects' documents, Administrator → all). Search and all
  listing operate over this permission-scoped set; no new per-role browse screens are
  added (reuse project views + search; admins also use audit reports).
- **Rationale**: Encodes FR-031 and the two clarifications directly into the service's
  authorization boundary, keeping IDOR protection centralized (Constitution IV/V).
- **Alternatives considered**: Per-role bespoke pages (rejected — clarification chose
  reuse); search limited to own uploads (rejected — uploader-name search needs broader
  scope).

## 8. Category storage & key types (technical constraints)

- **Decision**: `Document.DocumentId` is an integer identity (consistent with existing
  `User`/`Project` keys). `Category` is stored as a text value validated against the fixed
  list. `FileType` allows up to 255 chars (long Office MIME types). `FilePath` length
  accommodates GUID-based names.
- **Rationale**: Directly from the spec's technical constraints and existing schema
  conventions (all current entities use `int` keys).
- **Alternatives considered**: GUID primary key (rejected — inconsistent with existing
  tables); integer enum category column (rejected — spec requires text for simplicity).

## 9. Blazor file-upload component handling

- **Decision**: Use `InputFile` with a `@key` to force re-render after upload; copy
  `IBrowserFile` content into a `MemoryStream` immediately (extract name/size/contentType
  first), then clear the file reference. Pass the stream to `IFileStorageService`.
- **Rationale**: Prevents stream-disposal and reuse errors documented for Blazor Server
  uploads; keeps the page thin while the service does the work (Constitution V).
- **Alternatives considered**: Direct `OpenReadStream` passed deep into services
  (rejected — disposal/lifetime issues in Blazor Server).

## 10. Notifications & dashboard integration

- **Decision**: Reuse the existing `INotificationService` to create in-app notifications on
  share and on new project-document upload. Reuse `IDashboardService`/`Index.razor` to add
  a Recent Documents widget (last 5) and a document count.
- **Rationale**: Constitution V (reuse services, no bypass); FR-026, FR-029, FR-030.
- **Alternatives considered**: A separate notification mechanism (rejected — duplicates
  existing capability).

## Outstanding unknowns

None. All Technical Context fields are concrete and all spec clarifications are
incorporated.
