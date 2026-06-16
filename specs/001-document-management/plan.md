# Implementation Plan: Document Upload and Management

**Branch**: `001-document-management` | **Date**: 2026-06-16 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/001-document-management/spec.md`

## Summary

Add document upload and management to ContosoDashboard so employees can upload, organize,
search, preview, share, and manage work documents by category and project, with role-based
access. The approach extends the existing Blazor Server + EF Core architecture: a new
`Document`, `DocumentShare`, and `DocumentActivityLog` entity set in
`ApplicationDbContext`; a `DocumentService` that orchestrates validate → scan → authorize →
store → persist → notify; and two swappable infrastructure abstractions — `IFileStorageService`
(local filesystem now, Azure Blob later) and `IMalwareScanner` (stub now, real scanner later).
Files are stored outside `wwwroot` under `AppData/uploads/{userId}/{projectId|personal}/{guid}.{ext}`
and served through an authorized MVC controller endpoint. All functionality runs fully offline
on SQL Server LocalDB and the local disk.

## Technical Context

**Language/Version**: C# 12 / .NET 8.0  
**Primary Dependencies**: ASP.NET Core 8.0, Blazor Server, Entity Framework Core 8.0 (SqlServer provider), Bootstrap 5.3  
**Storage**: SQL Server LocalDB (document metadata, shares, activity log) + local filesystem (file bytes, outside `wwwroot`)  
**Testing**: Manual validation via quickstart scenarios + documented manual security tests (automated tests OPTIONAL per constitution; spec did not request them)  
**Target Platform**: ASP.NET Core server (Windows/Linux); modern browser client  
**Project Type**: Web application — single Blazor Server project (`ContosoDashboard/`)  
**Performance Goals**: Upload of ≤25 MB completes ≤30 s (typical network); document list loads ≤2 s for up to 500 documents; search returns ≤2 s; preview loads ≤3 s  
**Constraints**: Fully offline — no cloud/external service dependencies; files stored outside `wwwroot`; GUID-based storage paths generated before DB insert; file-type whitelist; 25 MB per-file cap; storage and scanning behind swappable interfaces  
**Scale/Scope**: Training-scale; up to 500 documents per list view; small concurrent user base; 5 user stories, 35 functional requirements

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Compliance in this plan |
|-----------|-------------------------|
| **I. Training-First Scope** (NON-NEGOTIABLE) | Simplest design that meets requirements; training-only shortcuts (stub malware scanner) are explicitly labeled with the production alternative documented. No production-only assumptions introduced. |
| **II. Offline-First, Local-Only** | Metadata in LocalDB, files on local disk; no network, paid services, or credentials required. DB auto-created/seeded via existing `EnsureCreated()`; upload root auto-created on startup. |
| **III. Infrastructure Abstraction** | `IFileStorageService` (local now / Azure Blob later) and `IMalwareScanner` (stub now / real later) defined before implementations and wired via DI; swapping implementations needs no changes to `DocumentService`, pages, or schema. |
| **IV. Security by Design** (NON-NEGOTIABLE) | Pages use `[Authorize]`; `DocumentService` performs its own authorization on every read/download/edit/delete/share (IDOR protection); GUID paths generated before DB insert; extension whitelist; files outside `wwwroot`; downloads/previews via authorized controller endpoint; no user-supplied filenames in paths; activity logging for audit. |
| **V. Clean Separation & Simplicity** | New entities in `Models/`, orchestration in `Services/DocumentService`, data access only through `ApplicationDbContext`, UI in `Pages/`. Two abstractions are each justified by an explicit requirement (FR-035 storage swap, FR-008 offline scan); no speculative generality. |

**Initial gate result**: PASS — no violations. Complexity Tracking left empty.

**Post-design re-check (after Phase 1)**: PASS — the design adds three entities, one
orchestrating service, two DI-swappable abstractions (each justified by an explicit
requirement), and one authorized file-serving controller (the minimal surface needed to
serve files stored outside `wwwroot`). No new abstractions or generality beyond
requirements (YAGNI upheld); security, offline, and layering principles remain satisfied.
No constitutional violations introduced.

## Project Structure

### Documentation (this feature)

```text
specs/001-document-management/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
│   ├── IFileStorageService.md
│   ├── IMalwareScanner.md
│   ├── IDocumentService.md
│   └── documents-controller.md
├── checklists/
│   └── requirements.md  # Spec quality checklist (already created)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
ContosoDashboard/
├── Models/
│   ├── Document.cs                 # NEW — document metadata entity (int key, text category)
│   ├── DocumentShare.cs            # NEW — share relationship (user or team recipient)
│   └── DocumentActivityLog.cs      # NEW — audit log entry for document actions
├── Data/
│   └── ApplicationDbContext.cs     # MODIFIED — add DbSets, relationships, indexes
├── Services/
│   ├── IDocumentService.cs / DocumentService.cs   # NEW — upload/list/search/share orchestration + authorization
│   └── Storage/
│       ├── IFileStorageService.cs                 # NEW — storage abstraction
│       ├── LocalFileStorageService.cs             # NEW — local filesystem implementation
│       ├── IMalwareScanner.cs                     # NEW — scan abstraction
│       └── StubMalwareScanner.cs                  # NEW — offline simulated scanner
├── Controllers/
│   └── DocumentsController.cs      # NEW — authorized download/preview endpoints (serve files outside wwwroot)
├── Pages/
│   ├── Documents.razor             # NEW — My Documents list, upload modal, edit/delete/share
│   ├── SharedWithMe.razor          # NEW — documents shared with the current user
│   ├── ProjectDetails.razor        # MODIFIED — project documents tab
│   ├── Tasks.razor                 # MODIFIED — attach/upload documents from a task
│   └── Index.razor                 # MODIFIED — Recent Documents widget + document count
├── Shared/
│   └── NavMenu.razor               # MODIFIED — add Documents / Shared with Me navigation
├── Program.cs                      # MODIFIED — register services, MapControllers, ensure upload root
└── appsettings.json                # MODIFIED — DocumentStorage options (root path, max size, allowed types)

AppData/
└── uploads/                        # NEW (runtime, gitignored) — file bytes outside wwwroot
    └── {userId}/{projectId|personal}/{guid}.{ext}
```

**Structure Decision**: Single Blazor Server web project (existing `ContosoDashboard/`).
The feature follows the established Models / Services / Data / Pages layering. Two new
folders are introduced: `Services/Storage/` for the storage and scan abstractions, and a
top-level `Controllers/` for the authorized file-serving endpoints (Blazor Server requires
a controller to stream files that live outside `wwwroot` while enforcing authorization).
`MapControllers()` is added to the existing pipeline.

## Complexity Tracking

> No constitutional violations — section intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
