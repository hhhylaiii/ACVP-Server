# Phase 0 Research: FIPS 203/204 Validation Web Tool

All Technical Context items were resolvable from the project planning docs
(`project-docs/planning/`), the working CLI demo (`fips-203-204-demo/`), and the existing
codebase (`gen-val/`). No open `NEEDS CLARIFICATION` items remain.

## R1. Drive the Gen/Val engine in-process (no CLI shell-out)

- **Decision**: Call the existing `IGenValInvoker` interface directly from the Web API —
  `CheckParameters(ParameterCheckRequest)`, `GenerateAsync(GenerateRequest, vsId)`,
  `ValidateAsync(ValidateRequest, vsId)`
  (`gen-val/src/generation/src/NIST.CVP.ACVTS.Libraries.Generation.Core/IGenValInvoker.cs`).
- **Rationale**: The interface is exactly the three operations the workflow needs
  (check → generate → validate); the planning doc confirms it is the de-risking spike for
  S0. In-process avoids per-request process spawn cost and fragile stdout parsing.
- **Alternatives considered**: Shell out to `GenValAppRunner` per request — rejected for
  the main flow (process overhead, brittle file/stdout coupling) but **retained as the
  golden-parity oracle in tests** (FR-007) and as the documented fallback if DI wiring
  proves hard (risk R1 fallback).

## R2. Orleans Silo as the unmodified crypto backend

- **Decision**: Run the existing `NIST.CVP.ACVTS.Orleans.ServerHost` as a separate
  container; the API connects as an Orleans client. Port the existing `AutofacConfig` /
  Orleans client wiring into the API host. Do not modify any grain or crypto code.
- **Rationale**: Constitution Principle III (upstream crypto untouched); the demo already
  proves the Silo computes all five modes correctly end-to-end.
- **Alternatives considered**: Embedding crypto into the API — rejected (duplicates/forks
  upstream, violates Principle III).

## R3. Long-running work → background jobs + polling

- **Decision**: Generation and validation run as background jobs identified by a job id;
  the client polls a status endpoint until `Succeeded`/`Failed`, then fetches results.
- **Rationale**: Largest in-scope sets (ML-DSA sigGen ~360, ML-KEM encapDecap ~165 cases,
  per the walkthrough) can exceed a safe synchronous HTTP window (risk R2). A job model
  keeps requests short and the UI responsive.
- **Alternatives considered**: Pure synchronous endpoints — acceptable only for tiny
  `check` calls; rejected for generate/validate. WebSockets/SignalR — more moving parts
  than needed for a low-concurrency internal tool; polling is sufficient (Principle I).

## R4. Frontend: React + Vite

- **Decision**: React 18 + TypeScript built with Vite, served as static assets by the API
  container.
- **Rationale**: A simple, card-based, non-expert SPA (select → generate → upload →
  report); large ecosystem; bundles to static files easy to host from ASP.NET Core.
- **Alternatives considered**: Blazor (keeps everything .NET, fewer context switches) —
  viable; React chosen for lighter client footprint and the team's stated default. Either
  satisfies the spec; this is an implementation choice, not a spec requirement.

## R5. Artifact storage and the data-privacy boundary

- **Decision**: Persist generated artifacts (`prompt`, `internalProjection`,
  `expectedResults`, `validation`) on the container filesystem under a per-job directory;
  never request or store IUT source code or private keys — only prompt/response JSON.
- **Rationale**: Mirrors the CLI's file model; satisfies FR-010/FR-011 data-privacy
  requirement ("資料不出門"). `expectedResults` stays server-side and is never shipped to
  the client (only the example response file is).
- **Alternatives considered**: SQLite history store — deferred to Backlog (WT-BL-001),
  not needed for MVP.

## R6. Packaging: Docker Compose (web + silo)

- **Decision**: `docker compose up` starts two services — the API/SPA container and the
  Orleans Silo container — with `MaxConcurrentWork` set below the host CPU count via
  configuration/environment.
- **Rationale**: FR-013 single startup action; cross-platform (risk R3); keeps data on the
  internal network.
- **Alternatives considered**: Manual multi-terminal startup (as in the demo) — rejected
  for non-expert operators.

## R7. Precise, safe upload errors

- **Decision**: Validate uploads structurally before invoking the engine; on failure
  return errors naming the offending `tcId` and field with a hint; map engine/Orleans
  exceptions to safe HTTP status codes without stack traces.
- **Rationale**: FR-008/FR-010 and Constitution Principle IV (no leakage); addresses the
  company-handoff format-mismatch risk (R6).
- **Alternatives considered**: Surfacing raw engine exceptions — rejected (leaks internals,
  unhelpful to non-experts).

## R8. Company integration support pack

- **Decision**: Ship per-algorithm sample harnesses modeled on the demo's `iut_mlkem.py` /
  `iut_mldsa.py` shape, but with the answer source reduced to a single
  "call your module here" line, plus a field-mapping doc (input/output fields, encoding,
  byte length) and example `responses.json` per mode.
- **Rationale**: FR-014/SC-006; the demo harnesses already establish the read-prompt →
  write-response skeleton, so only the module call differs for a real IUT.
- **Alternatives considered**: Documentation only — rejected; a runnable skeleton reduces
  the handoff to one line and catches format drift early (risk R6).
