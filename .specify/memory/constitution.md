<!--
SYNC IMPACT REPORT
==================
Version change: (template/unversioned) → 1.0.0
Rationale: Initial ratification of the ContosoDashboard constitution, replacing all
template placeholders with concrete, project-specific principles derived from the
documented architecture and security model (README.md, StakeholderDocs).

Modified principles:
- [PRINCIPLE_1_NAME] → I. Training-First Scope (NON-NEGOTIABLE)
- [PRINCIPLE_2_NAME] → II. Offline-First, Local-Only Operation
- [PRINCIPLE_3_NAME] → III. Infrastructure Abstraction (Cloud Migration Ready)
- [PRINCIPLE_4_NAME] → IV. Security by Design (NON-NEGOTIABLE)
- [PRINCIPLE_5_NAME] → V. Clean Separation of Concerns & Simplicity

Added sections:
- Security & Compliance Constraints (Section 2)
- Development Workflow (Section 3)

Removed sections: None (all template slots populated)

Templates requiring updates:
- ✅ .specify/templates/plan-template.md (Constitution Check gate is generic;
     compatible — no change required)
- ✅ .specify/templates/spec-template.md (no constitution-specific tokens; compatible)
- ✅ .specify/templates/tasks-template.md (test tasks optional; compatible)

Follow-up TODOs: None. Ratification date set to first formal adoption.
-->

# ContosoDashboard Constitution

## Core Principles

### I. Training-First Scope (NON-NEGOTIABLE)

ContosoDashboard exists solely as a teaching vehicle for Spec-Driven Development (SDD)
using the GitHub Spec Kit. Every change MUST preserve the project's value as a learning
artifact and MUST NOT introduce production-only assumptions.

- The codebase MUST NOT be positioned, documented, or extended as production-ready.
- Features MUST demonstrate good, industry-recognizable practices, simplified for a
  training context, with any limitations explicitly documented.
- Known shortcuts (e.g., mock authentication) MUST be clearly labeled as training-only
  with a note describing the production-grade alternative.

**Rationale**: The project's primary deliverable is learning. Drifting toward
production complexity defeats its purpose and confuses learners.

### II. Offline-First, Local-Only Operation

The application MUST run completely offline with no external service dependencies or
cloud subscriptions required.

- Data persistence MUST default to SQL Server LocalDB; file-based features MUST default
  to the local filesystem.
- A first-run experience MUST create and seed required data automatically so learners
  can start immediately.
- New features MUST NOT require network access, paid services, or credentials to run in
  the training environment.

**Rationale**: Maximizing availability for learners — anywhere, anytime, without cost —
is a core requirement of the training experience.

### III. Infrastructure Abstraction (Cloud Migration Ready)

Every infrastructure dependency (storage, database access patterns, authentication,
external services) MUST sit behind an interface abstraction wired through dependency
injection.

- Local implementations (e.g., `LocalFileStorageService`) MUST be swappable for cloud
  implementations (e.g., `AzureBlobStorageService`) without changing business logic, UI,
  or database schema.
- Migration to Azure equivalents MUST require only configuration and implementation
  swaps — never edits to consuming code.
- Interfaces MUST be defined before their concrete implementations when introducing a new
  infrastructure concern.

**Rationale**: Teaching proper abstraction and dependency injection is an explicit goal,
and it keeps the documented offline-to-Azure migration path real and demonstrable.

### IV. Security by Design (NON-NEGOTIABLE)

Security controls MUST be enforced in depth even though authentication is mocked for
training.

- All protected pages MUST require authorization via the `[Authorize]` attribute, and
  role-based access control MUST be honored.
- Services MUST perform their own authorization checks to prevent Insecure Direct Object
  Reference (IDOR) — each user MUST see only data they are authorized to access.
- File handling MUST use GUID-based unique paths generated BEFORE database insertion,
  validate file types against a whitelist, store uploads outside `wwwroot`, and never use
  user-supplied filenames directly in paths.
- Defense in depth (middleware, page attributes, service checks, security headers) MUST
  be maintained; security regressions MUST be treated as blocking defects.

**Rationale**: The application teaches secure patterns; weakening enforcement would
teach the wrong lessons and introduce real vulnerabilities into shared training material.

### V. Clean Separation of Concerns & Simplicity

Code MUST maintain clear layering — Models, Services, Data, and Pages — and MUST favor
the simplest solution that satisfies the requirement.

- Business logic MUST live in the Services layer; Pages MUST NOT bypass services to reach
  the data layer directly.
- Data access MUST flow through the Entity Framework Core `ApplicationDbContext`.
- New abstractions, options, or generality MUST be justified by a current requirement
  (YAGNI); speculative complexity MUST be avoided.

**Rationale**: Readable, well-layered, minimal code is easier for learners to follow and
mirrors the separation-of-concerns lesson the project is built to teach.

## Security & Compliance Constraints

The following constraints apply to all contributions:

- **Technology baseline**: ASP.NET Core 8.0, Blazor Server, Entity Framework Core,
  SQL Server LocalDB, Bootstrap 5.3. Changes MUST stay within this stack unless a spec
  explicitly amends it.
- **Mock authentication boundary**: The cookie-based, password-less mock auth is for
  training only. Any documentation MUST reference production identity providers
  (Microsoft Entra ID, Identity Server, Auth0) and the controls they require (password
  hashing, MFA, OAuth 2.0/OpenID Connect) as the production path.
- **Data isolation**: User isolation and RBAC behavior MUST remain verifiable via the
  documented manual security tests (authentication required, user isolation, IDOR
  protection, role-based features).
- **No secrets in source**: Credentials, keys, or connection strings to real services
  MUST NOT be committed; training defaults MUST remain local and non-sensitive.

## Development Workflow

ContosoDashboard development follows the Spec-Driven Development workflow:

- Features begin from a specification (see `StakeholderDocs/` and Spec Kit artifacts in
  `specs/`); implementation MUST trace back to documented requirements.
- Plans MUST pass the Constitution Check gate before implementation; any deviation MUST be
  justified explicitly in the plan's complexity tracking.
- Tests are OPTIONAL and included only when a specification requests them; when present,
  they MUST cover the documented security behaviors and core acceptance criteria.
- Changes MUST keep the application runnable via `dotnet run` with automatic database
  creation and seeding on first run.

## Governance

This constitution supersedes other practices for the ContosoDashboard training project.

- **Amendments**: Proposed changes MUST be documented, justified against the training
  mission, and reflected in dependent Spec Kit templates where applicable.
- **Versioning policy**: Semantic versioning applies to this constitution.
  MAJOR for backward-incompatible governance/principle removals or redefinitions; MINOR
  for newly added or materially expanded principles/sections; PATCH for clarifications and
  non-semantic refinements.
- **Compliance review**: All plans and implementations MUST verify compliance with these
  principles; the Security by Design and Training-First Scope principles are
  non-negotiable and MUST NOT be waived.
- **Runtime guidance**: Use `README.md` and the Spec Kit command templates under
  `.specify/templates/` for day-to-day development guidance.

**Version**: 1.0.0 | **Ratified**: 2026-06-16 | **Last Amended**: 2026-06-16
