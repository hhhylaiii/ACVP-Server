# Implementation Plan: FIPS 203/204 Validation Web Tool

**Branch**: `001-validation-web-tool` | **Date**: 2026-06-29 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/001-validation-web-tool/spec.md`

## Summary

Build a self-hosted web tool that lets a non-cryptographer operator generate ACVP
test-vector prompts for FIPS 203 (ML-KEM) and FIPS 204 (ML-DSA), hand them to their
own module (IUT) offline, upload the produced responses, and read a pass/fail report —
all from a browser, no CLI. The tool reuses the repository's existing Gen/Val engine
through the in-process `IGenValInvoker` interface (`CheckParameters` / `GenerateAsync` /
`ValidateAsync`) backed by the existing Orleans Silo, and adds a thin ASP.NET Core 8 Web
API + a React (Vite) SPA on top. Upstream cryptographic code is not modified. The whole
package starts with a single `docker compose up`, keeping all test data on the operator's
own network.

## Technical Context

**Language/Version**: C# / .NET 8 (backend, matches the existing repo); TypeScript +
React 18 via Vite (frontend SPA)

**Primary Dependencies**: ASP.NET Core 8 Web API; existing `gen-val` Generation/Common
libraries via project reference (`IGenValInvoker`, request/response models); Microsoft
Orleans client to the existing `NIST.CVP.ACVTS.Orleans.ServerHost` Silo; React + Vite +
TypeScript; Docker Compose for packaging

**Storage**: Filesystem for generated artifacts (prompt / internalProjection /
expectedResults / validation) keyed by job id; optional SQLite for run history
(deferred — out of MVP, see Backlog)

**Testing**: xUnit + FluentAssertions (backend unit); ASP.NET Core
`WebApplicationFactory` (API integration); golden-parity tests comparing the tool's
disposition against the `GenValAppRunner` CLI for the same inputs; Vitest +
Testing Library (frontend unit) and a thin Playwright smoke (optional)

**Target Platform**: Linux and Windows hosts via Docker Compose; runs entirely on the
partner company's internal network (no internet at runtime)

**Project Type**: Web application (frontend + backend) layered on the existing Orleans
crypto backend

**Performance Goals**: Cope with the largest in-scope vector sets (e.g. ML-DSA sigGen
~360 cases, ML-KEM encapDecap ~165 cases) by running generation/validation as background
jobs with a status-polling endpoint, so no HTTP request blocks long enough to time out;
interactive screens respond in well under 1 s for selection/report rendering

**Constraints**: Test data MUST NOT leave the operator's environment (no external runtime
dependency); IUT source code and private keys are never requested, stored, or transmitted;
`MaxConcurrentWork` is kept below the host CPU count (existing engine requirement); upload
size is bounded; concurrent jobs are bounded to host capacity

**Scale/Scope**: Single-company internal use, a handful of concurrent operators; exactly
5 algorithm-mode combinations (ML-KEM keyGen/encapDecap; ML-DSA keyGen/sigGen/sigVer)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Assessment | Status |
|-----------|------------|--------|
| I. MVP-First, No Overdesign | Scope frozen to 5 modes; SQLite history, accounts, multi-tenancy explicitly deferred; thin API over existing engine | ✅ PASS |
| II. Complete & Executable | Plan targets a runnable `docker compose up` stack with real generate/validate paths; no stubs in committed logic | ✅ PASS |
| III. Respect Existing Architecture | Reuse `gen-val` libraries + `IGenValInvoker` + existing Orleans Silo; **upstream crypto untouched**; framework-native (ASP.NET Core DI, Orleans client). New deps (ASP.NET Core, React/Vite, Docker Compose) justified below | ✅ PASS |
| IV. Robustness & Security | Nullable reference types on; config via `appsettings`/env (no hardcoded secrets); engine/Orleans calls wrapped with explicit exception → HTTP status mapping; data-privacy model enforced (no IUT source/keys) | ✅ PASS |
| V. TDD & Test Synchronization | Tests written first (xUnit), golden-parity tests derived from requirements (FR-007); coverage ≥80% floor; test edits gated | ✅ PASS |
| VI. Clear Naming & English Docs | English code naming + XML doc comments; user-facing operator manual may be Traditional Chinese/English for the company | ✅ PASS |
| VII. Documented Design & Backlog | This plan + data-model + contracts + quickstart; deferred items tracked in Backlog section | ✅ PASS |
| VIII. Documentation Synchronization | Contract in `contracts/openapi.yaml`, quickstart, and README kept in sync with changes | ✅ PASS |

**Justified new dependencies** (Principle III): ASP.NET Core 8 (already within the .NET 8
stack the repo uses) for the Web API; React + Vite for the browser SPA (no framework-native
.NET alternative gives a non-expert SPA without added weight — Blazor was considered, see
research.md); Docker Compose to satisfy the single-command self-host requirement (FR-013).
No changes to upstream `gen-val` cryptographic projects.

*Post-Phase 1 re-check*: Design artifacts (data-model.md, contracts/openapi.yaml,
quickstart.md) introduce no new violations; all gates remain ✅ PASS.

## Project Structure

### Documentation (this feature)

```text
specs/001-validation-web-tool/
├── plan.md              # This file (/speckit-plan output)
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   └── openapi.yaml     # Phase 1 output — REST contract
├── checklists/
│   └── requirements.md  # Spec quality checklist (from /speckit-specify)
└── tasks.md             # Phase 2 output (/speckit-tasks — NOT created here)
```

### Source Code (repository root)

A new self-contained `web-tool/` tree is added at the repository root; it references the
existing `gen-val` projects but does not modify them.

```text
web-tool/
├── backend/
│   ├── src/
│   │   └── Acvp.WebTool.Api/          # ASP.NET Core 8 Web API + SPA host
│   │       ├── Endpoints/             # Minimal API endpoint groups (check/generate/validate/jobs)
│   │       ├── Services/              # GenValService (wraps IGenValInvoker), JobQueue, ArtifactStore
│   │       ├── Models/                # Request/response DTOs (records)
│   │       ├── Validation/            # Upload + selection validators, error mapping
│   │       ├── Options/               # Strongly typed options (engine, limits, paths)
│   │       └── Program.cs
│   └── tests/
│       ├── Acvp.WebTool.Api.UnitTests/
│       └── Acvp.WebTool.Api.IntegrationTests/   # WebApplicationFactory + golden parity vs CLI
├── frontend/                          # React + Vite + TypeScript SPA
│   ├── src/
│   │   ├── pages/                     # Select → Generate → Upload → Report flow
│   │   ├── components/
│   │   ├── api/                       # typed client derived from openapi.yaml
│   │   └── lib/
│   └── tests/                         # Vitest + Testing Library
├── integration-pack/                  # US3 deliverable handed to the company
│   ├── harness_mlkem.py               # one-line-to-fill response producer
│   ├── harness_mldsa.py
│   ├── field-mapping.md               # per-mode prompt/response field + encoding + length
│   └── sample-responses/              # example responses.json per mode
└── deploy/
    ├── Dockerfile.api                 # builds API (+ bundled SPA)
    └── docker-compose.yml             # web + existing Orleans Silo
```

**Structure Decision**: Web application (frontend + backend) plus the existing Orleans
Silo as an unmodified crypto backend. The new code is isolated under `web-tool/` so the
fork stays cleanly separable from upstream `gen-val/`. The API references the existing
Generation/Common projects directly (in-process `IGenValInvoker`), avoiding any CLI
shell-out in the main flow (the CLI is retained only as the golden-parity oracle in tests).

## Complexity Tracking

> No constitution violations require justification. The only additions beyond the existing
> stack (ASP.NET Core, React/Vite, Docker Compose) are required by the spec's browser-based,
> single-command self-host requirements and are recorded under the Constitution Check above.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| (none)    | —          | —                                   |

## Backlog (deferred / follow-up)

- **WT-BL-001** (Dev A): Optional SQLite-backed run history + history list UI. Deferred to keep MVP thin.
- **WT-BL-002** (Dev B): Advanced ML-DSA option surface (deterministic, externalMu, preHash, hashAlgs) behind an "advanced" toggle. MVP ships safe defaults only.
- **WT-BL-003** (Dev A): Streaming/chunked upload for very large response files beyond the configured size limit.
- **WT-BL-004** (Dev C): Expand golden-parity test matrix to additional parameter sets and corrupted-answer fuzz cases.
- **WT-BL-005** (Dev A): Pin upstream `gen-val` to a specific commit/tag to guard against breaking changes (risk R8).
- **WT-BL-006** (future): CMVP-format submission report export; additional PQC algorithms (SLH-DSA/LMS); hosted SaaS option. Out of this term's scope.
