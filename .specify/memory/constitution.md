<!--
SYNC IMPACT REPORT
==================
Version change: 1.0.0 → 1.1.0
Bump rationale: The 1.0.0 text was pasted from an unrelated cookiecutter-django /
AI-auto-reply project. This amendment re-scopes every principle to the actual
repository — a FIPS 203/204 validation web tool forked from NIST ACVP-Server
(C# / .NET 8, ASP.NET Core, Orleans, React/Vite, Docker Compose). Principle
identities and count are unchanged; their guidance was materially retargeted
(stack, architecture, security/data-privacy model, doc paths) → MINOR.

Modified principles (content retargeted; names unchanged):
  III. Respect Existing Architecture — Django/DRF/Celery → NIST ACVP-Server
       upstream + gen-val libraries, ASP.NET Core, Orleans; do not modify upstream
       crypto.
  IV.  Robustness & Security — Python type hints / "simulated AI API" /
       "conversation data" → C# nullable types, Orleans/IGenValInvoker calls,
       IUT data-stays-on-prem (資料不出門) model.
  V.   Test-Driven Development — pytest → .NET test projects (xUnit/NUnit).
  VI.  Clear Naming & English Documentation — clarified: code in English,
       user-facing product docs may be Traditional Chinese for the target users.
  VII. / VIII. — flow + doc paths retargeted (generate→collect→grade;
       appsettings/sharedappsettings + .NET XML doc comments).
Added principles: none.
Removed principles: none.
Added sections: none (Workflow examples updated to project-appropriate names).

Templates requiring updates:
  ✅ .specify/templates/plan-template.md — generic Constitution Check; compatible.
  ✅ .specify/templates/spec-template.md — compatible.
  ✅ .specify/templates/tasks-template.md — test-mandatory note (Principle V)
     already applied; still valid.
  ✅ .specify/templates/checklist-template.md — compatible.

Follow-up TODOs: none (PROJECT_NAME resolved from README.md).
-->

# FIPS 203/204 Validation Web Tool Constitution

<!-- Product name (Traditional Chinese): FIPS 203/204 演算法驗證網頁工具.
     A self-hosted web tool that lets non-cryptographer users validate their
     module (IUT) implementations of FIPS 203 (ML-KEM) and FIPS 204 (ML-DSA)
     without any CLI, reusing NIST ACVP-Server's Gen/Val engine and Orleans Silo.
     Forked from usnistgov/ACVP-Server; upstream cryptographic code is NOT
     modified. -->

## Core Principles

### I. MVP-First, No Overdesign

Every feature MUST target the Minimum Viable Product that satisfies the stated
requirement, within the frozen scope (FIPS 203 ML-KEM keyGen/encapDecap at
512/768/1024 and FIPS 204 ML-DSA keyGen/sigGen/sigVer at 44/65/87). Out-of-scope
items — other algorithms, cloud multi-tenancy, and an account/permission system —
MUST NOT be built. Code MUST be high-quality and testable, but speculative
abstraction, unused configurability, and premature optimization are PROHIBITED.
When a simpler design meets the requirement, the simpler design MUST be chosen.

**Rationale**: The task scope is bounded and frozen for the term; clarity and
working behavior outweigh architectural flourish. Overdesign hides bugs and slows
review.

### II. Complete & Executable Implementation

Delivered code MUST be fully working and runnable end-to-end (`docker compose up`
brings up the web tool against the Orleans Silo). Placeholder artifacts such as
`TODO`/`FIXME` markers in committed logic, `throw new NotImplementedException()`,
empty method bodies, or stubbed returns that fake behavior are PROHIBITED in
committed business logic. Every code path a feature claims to support — generate
prompt, collect IUT response, grade — MUST actually execute.

**Rationale**: Reviewers and graders evaluate running systems; partial scaffolding
misrepresents progress and breaks downstream integration.

### III. Respect Existing Architecture

This repository is a fork of NIST `usnistgov/ACVP-Server`. The upstream
cryptographic implementations under `gen-val/` MUST NOT be modified; the web tool
is built as a new layer on top. Implementation MUST follow the existing solution
structure and .NET conventions, reuse the `gen-val` Generation/Common libraries,
and use the existing Orleans Silo as the crypto backend (via `IGenValInvoker` /
the oracle bridge) rather than re-implementing crypto. New frameworks, heavyweight
dependencies, or alternative project layouts MUST NOT be introduced without
explicit justification recorded in the plan. Framework-native mechanisms
(ASP.NET Core controllers/minimal APIs, built-in DI, Orleans grains) MUST be
preferred over custom machinery.

**Rationale**: Staying aligned with upstream and the .NET/Orleans structure keeps
the fork mergeable, keeps the codebase navigable for a multi-person PR workflow,
and avoids dependency sprawl.

### IV. Robustness & Security

All C# code MUST use explicit types and enable/respect nullable reference types on
public interfaces. Secrets and sensitive configuration MUST NEVER be hardcoded;
they MUST be sourced from configuration/environment (`appsettings*.json`,
`sharedappsettings.json`, environment variables). Calls to external/back-end
services — the Orleans Silo and the Gen/Val invoker — MUST catch exceptions
explicitly and translate failures into reasonable HTTP status codes. Errors MUST
NOT be silently swallowed, and error responses MUST NOT leak sensitive detail.

Data-privacy model is non-negotiable: the IUT (the company's module) runs on the
company side and the web tool only issues prompts, collects responses, and grades.
The tool MUST NOT request, persist, or transmit IUT source code or private keys
("資料不出門").

**Rationale**: The tool runs on a company intranet over a partner's
cryptographic module; predictable failure handling, secret hygiene, and keeping
sensitive material on-premises are core requirements.

### V. Test-Driven Development & Test Synchronization

Business logic MUST be developed test-first using the project's .NET test projects
(xUnit/NUnit, as established per solution). The failing test that specifies the
new behavior MUST be written before the implementation that satisfies it
(RED → GREEN → REFACTOR), and the test MUST be derived from the requirement, NOT
from a pre-existing implementation. Any modification to business logic MUST be
accompanied by its tests in the same change; a change that alters behavior without
a corresponding test is incomplete and MUST NOT be merged. Tests MUST validate the
changed logic, not merely assert that code runs.

Coverage is a diagnostic, not a goal: the project's minimum (≥80%) is a floor, and
tests MUST NOT be authored or shaped merely to inflate a coverage number.

Modifying an existing test is a high-trust action. When the author judges that an
existing test must be changed or deleted to land a change, that judgment MUST be
surfaced to the maintainer for an explicit decision BEFORE the test is altered.
Silently editing tests to match new code — or to turn a red bar green — is
PROHIBITED.

**Rationale**: Tests are the contract that protects behavior across a
collaborative PR-and-review process. Writing them after the fact lets them
rubber-stamp whatever the code happens to do, and quietly tuning them to fit the
implementation or to chase coverage destroys their value as an independent check —
so both the test-first ordering and the approval gate on test edits are
non-negotiable.

### VI. Clear Naming & English Documentation

Variable, function, and class names MUST be clear, descriptive English. Code
comments and API doc comments (C# XML `///` docs) MUST be written in English and
kept concise, and MUST explain intent ("why"), not restate the code ("what").
User-facing product documentation (e.g. `README.md`, operator guides) MAY be
written in Traditional Chinese for the non-technical target users.

**Rationale**: English naming and code-level documentation keep the codebase
accessible to all collaborators and align with the upstream conventions, while the
product docs meet the actual readers — the partner company's non-technical
operators — where they are.

### VII. Documented Design & Optimization Backlog

The deliverable MUST include concise documentation explaining how the modules
interact: the data model, the validation flow (generate prompt → collect IUT
response → grade), and the API surface. A maintained backlog of known optimization
items and follow-up work MUST be kept, with each item phrased as an actionable
ticket (what to improve and the responsible owner/role). Deferred items and any
descoped features MUST be recorded there rather than silently dropped.

**Rationale**: The project is graded on architecture clarity, maintainability, and
the ability to identify future improvements; explicit design notes and a triaged
backlog make those decisions reviewable and demonstrate forward planning without
scope creep.

### VIII. Documentation Synchronization

Every change MUST include an explicit assessment of whether documentation needs
updating, and any documentation the change renders inaccurate MUST be updated
within the same change. The assessment MUST cover, where relevant: the API
contract (`specs/**/contracts/openapi.yaml`), feature specs/plans/quickstart under
`specs/`, the product and operations docs under `docs/` and `project-docs/`, the
top-level `README.md`, configuration references (`appsettings*.json`,
`sharedappsettings.json`, environment variables), and inline XML doc comments. A
change that alters behavior, interfaces, configuration, or workflow without
reconciling the affected documentation is incomplete and MUST NOT be merged. When
a change genuinely requires no documentation update, that conclusion MUST be the
result of a deliberate check, not a silent omission.

**Rationale**: Documentation is part of the deliverable's reviewable surface
(Principle VII); silently drifting docs mislead reviewers, operators, and future
contributors. Pairing every change with a documentation-impact check keeps the
design notes, API contract, and runbooks trustworthy — the documentation analogue
of Test Synchronization (Principle V).

## Development Workflow & Requirements Traceability

**Collaboration model**: Work is delivered through Pull Requests with PR review,
as described in the task brief. Each PR MUST be focused and reviewable.

### Branching & Merge Discipline

The project uses a single long-lived release branch (`main`) with short-lived
feature branches merged back after verification:

- **Feature branches MUST be short-lived.** A feature branch's lifetime MUST NOT
  exceed one iteration cycle. If a change grows large or becomes a significant
  refactor, it MUST be merged back to `main` promptly rather than accumulating.
- **Sync daily.** Changes on `main` MUST be merged into each active feature branch
  at least daily to keep branches close to `main` and minimize merge conflicts.
- **Merge back only after verification.** A feature branch MUST be merged to `main`
  only once the feature is verified (tests pass, review approved). Features MUST
  NOT be rushed in half-done ("不趕鴨子上架"); incomplete work stays out of `main`.
- **Limited branch count.** The number of concurrent feature branches MUST NOT
  exceed the number of features actively under development — no stale or
  speculative branches.
- **Frequent integration.** Frequent, small merges to `main` are preferred over
  large, infrequent ones.
- **Branch naming MUST be feature-based and uniform.** Branches MUST follow
  `NNN-short-name`, where `NNN` is a zero-padded sequential number and the suffix
  is a concise, lowercase, hyphen-separated English description
  (e.g. `001-validation-web-tool`). The first branch of a feature matches its Spec
  Kit feature directory name. Names MUST describe the work, not a person, date, or
  ticket id alone.
- **One branch (and one PR) per user story.** A feature's user stories MUST each
  be delivered on their own short-lived branch and merged via a separate, focused
  PR, so a reviewer can evaluate one story at a time. Each user-story branch takes
  the next zero-padded sequential number with a story-scoped suffix that ties it
  to the feature (e.g. for feature `001-validation-web-tool`:
  `002-us1-mlkem-keygen`, `003-us2-mldsa-siggen`, `004-us3-result-report`).
  Shared setup/foundational work that every story depends on MUST land first (on
  the feature's base branch) before story branches fork from `main`.

## Governance

This constitution supersedes all other development practices for this project.
When guidance conflicts, this document wins.

- **Authority**: All PRs and reviews MUST verify compliance with the principles
  above. A reviewer MUST block a PR that violates a MUST/PROHIBITED rule until the
  violation is resolved or an explicit, recorded justification is added to the
  plan's Complexity Tracking.
- **Amendment procedure**: Amendments MUST be proposed via PR that edits this
  file, state the rationale, and update the version and dates below. Dependent
  templates under `.specify/templates/` and affected docs MUST be reconciled in
  the same change (per Principle VIII).
- **Versioning policy**: This constitution is versioned with semantic versioning.
  MAJOR = backward-incompatible governance/principle removals or redefinitions;
  MINOR = a new principle/section or materially expanded/retargeted guidance;
  PATCH = clarifications, wording, or non-semantic refinements.
- **Compliance review**: Every PR description MUST include the Test
  Synchronization (Principle V) and Documentation Synchronization (Principle VIII)
  assessments. Use the Spec Kit plan/spec/tasks artifacts as the runtime guidance
  for keeping work aligned with these principles.

**Version**: 1.1.0 | **Ratified**: 2026-06-29 | **Last Amended**: 2026-06-29
