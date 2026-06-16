# Phase 1 Data Model: Document Upload and Management

Derived from the spec's Key Entities and Functional Requirements. Entities follow existing
ContosoDashboard conventions: integer identity keys, `System.ComponentModel.DataAnnotations`
attributes, EF Core navigation properties, and `DateTime.UtcNow` defaults. New types are
added to `ApplicationDbContext` with relationships and indexes mirroring existing patterns.

## Entity: Document

Represents an uploaded file plus its metadata.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `DocumentId` | int | PK, identity | Integer key (consistent with User/Project) — FR per constraints |
| `Title` | string | Required, MaxLength 255 | Defaults to filename on multi-upload (FR-005) |
| `Description` | string? | MaxLength 2000 | Optional (FR-006) |
| `Category` | string | Required, MaxLength 100 | Text value from fixed list (FR-005); not an int enum |
| `Tags` | string? | MaxLength 500 | Optional, comma-separated custom tags (FR-006) |
| `FileName` | string | Required, MaxLength 255 | Original filename for display/download |
| `FileType` | string | Required, MaxLength 255 | MIME type — 255 accommodates Office types |
| `FileSizeBytes` | long | Required | Captured automatically (FR-007) |
| `FilePath` | string | Required, MaxLength 500 | Relative GUID-based storage path; unique |
| `UploadedByUserId` | int | Required, FK → User | Uploader (FR-007) |
| `ProjectId` | int? | FK → Project, nullable | Optional association (FR-006) |
| `TaskId` | int? | FK → TaskItem, nullable | Set when uploaded from a task (FR-028) |
| `UploadedDate` | DateTime | Required, default UtcNow | Captured automatically (FR-007) |
| `UpdatedDate` | DateTime | Required, default UtcNow | Updated on metadata edit / file replace |

**Relationships**
- `Document` *→ 1* `User` (uploader), `OnDelete(Restrict)`.
- `Document` *→ 0..1* `Project`, `OnDelete(SetNull)`.
- `Document` *→ 0..1* `TaskItem`, `OnDelete(SetNull)`.
- `Document` *→ many* `DocumentShare`, `OnDelete(Cascade)`.
- `Document` *→ many* `DocumentActivityLog`, `OnDelete(Cascade)`.

**Indexes**: `UploadedByUserId`; `ProjectId`; unique index on `FilePath`.

**Validation rules**
- `Category` MUST be one of: Project Documents, Team Resources, Personal Files, Reports,
  Presentations, Other (FR-005).
- `FileType`/extension MUST be in the allowed set: PDF, Word, Excel, PowerPoint, plain
  text, JPEG, PNG (FR-002).
- `FileSizeBytes` MUST be ≤ 25 MB (FR-003).
- `FilePath` MUST be unique and GUID-based; never derived from the user-supplied filename
  (FR-011, FR-012).

**State transitions**
- *Created* on successful upload (file written → row inserted).
- *Metadata updated* (title/description/category/tags) — `UpdatedDate` refreshed (FR-021).
- *File replaced* — new GUID file written, old file deleted, `FilePath`/`FileType`/
  `FileSizeBytes`/`UpdatedDate` updated (FR-022).
- *Deleted* (hard delete) — row removed and file deleted after confirmation (FR-023).

## Entity: DocumentShare

Represents a sharing grant to a specific user or a team (department).

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `DocumentShareId` | int | PK, identity | |
| `DocumentId` | int | Required, FK → Document | |
| `SharedWithUserId` | int? | FK → User, nullable | Set for user-target shares (FR-025) |
| `SharedWithTeam` | string? | MaxLength 100, nullable | Department/team name for team shares (FR-025) |
| `SharedByUserId` | int | Required, FK → User | Owner who shared |
| `SharedDate` | DateTime | Required, default UtcNow | |

**Relationships**
- `DocumentShare` *→ 1* `Document`, `OnDelete(Cascade)`.
- `DocumentShare` *→ 0..1* `User` (recipient), `OnDelete(Restrict)`.
- `DocumentShare` *→ 1* `User` (sharer), `OnDelete(Restrict)`.

**Indexes**: `DocumentId`; `SharedWithUserId`; `SharedWithTeam`.

**Validation rules**
- Exactly one of `SharedWithUserId` or `SharedWithTeam` MUST be set.
- Team shares grant access dynamically by matching a user's current `Department` to
  `SharedWithTeam` (FR-025, clarified — membership-following).

## Entity: DocumentActivityLog

Audit record of a document action (FR-032, FR-033).

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `DocumentActivityLogId` | int | PK, identity | |
| `DocumentId` | int? | FK → Document, nullable | Nullable so log survives hard delete |
| `Action` | string | Required, MaxLength 50 | "Upload", "Download", "Delete", "Share" |
| `PerformedByUserId` | int | Required, FK → User | Acting user |
| `Timestamp` | DateTime | Required, default UtcNow | |
| `Details` | string? | MaxLength 500 | Optional context (e.g., share target) |

**Relationships**
- `DocumentActivityLog` *→ 0..1* `Document`, `OnDelete(SetNull)` (preserve audit trail
  after deletion).
- `DocumentActivityLog` *→ 1* `User`, `OnDelete(Restrict)`.

**Indexes**: `DocumentId`; `PerformedByUserId`; `Action`.

## Reference data: Category (fixed list)

Not a database table — a fixed, code-defined list validated on write and presented in the
upload UI:

`Project Documents`, `Team Resources`, `Personal Files`, `Reports`, `Presentations`,
`Other`.

## Modifications to existing entities

- **`User`**: add `ICollection<Document> UploadedDocuments` navigation (optional, for EF
  relationship clarity). `Department` already exists and serves as the team for sharing.
- **`Project`**: add `ICollection<Document> Documents` navigation.
- **`TaskItem`**: add `ICollection<Document> Documents` navigation (FR-028).
- **`ApplicationDbContext`**: add `DbSet<Document>`, `DbSet<DocumentShare>`,
  `DbSet<DocumentActivityLog>`; configure relationships, indexes, and the unique
  `FilePath` index in `OnModelCreating`. No changes to existing seed data required (the
  feature ships with zero seeded documents).

## Permission-derived access set (computed, not stored)

For a requesting user `u`, the set of accessible documents is:

```
own        = documents where UploadedByUserId == u.UserId
projectDocs = documents where ProjectId in (projects u manages or is a member of)
sharedDocs = documents with a DocumentShare where SharedWithUserId == u.UserId
             OR SharedWithTeam == u.Department
elevated   = if u is TeamLead    → documents uploaded by members of u's team/department
             if u is ProjectMgr  → documents in projects u manages
             if u is Admin       → all documents
accessible = own ∪ projectDocs ∪ sharedDocs ∪ elevated
```

This set is enforced centrally by `DocumentService` for list, search, download, preview,
edit, delete, and share authorization (FR-010, FR-018, FR-031; clarified scope).
