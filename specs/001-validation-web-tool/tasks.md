---
description: "Task list for FIPS 203/204 Validation Web Tool implementation"
---

# Tasks: FIPS 203/204 Validation Web Tool

**Input**: Design documents from `/specs/001-validation-web-tool/`

**Prerequisites**: plan.md (required), spec.md (user stories), research.md, data-model.md, contracts/openapi.yaml, quickstart.md

**Tests**: Per Constitution Principle V (Test-Driven Development & Test Synchronization), tests for business logic are MANDATORY and written test-first (RED → GREEN → REFACTOR). The failing test task precedes the implementation task it covers — this applies to foundational business logic (job state machine, artifact storage boundary) as well as per-story logic. Purely declarative scaffolding (project init, static config, deliverable docs) omits tests where no business logic exists.

**Terminology**: The downloadable deliverable from a generate job is called the **prompt package** throughout (the literal endpoint path remains `/api/jobs/{jobId}/prompt-package`). It contains `prompt.json` + an example `responses.json` + human-readable instructions.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete work)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)
- All paths are relative to the repository root and assume the `web-tool/` tree from plan.md

## Path Conventions

- Backend API: `web-tool/backend/src/Acvp.WebTool.Api/`
- Backend tests: `web-tool/backend/tests/Acvp.WebTool.Api.UnitTests/`, `web-tool/backend/tests/Acvp.WebTool.Api.IntegrationTests/`
- Frontend: `web-tool/frontend/src/`, `web-tool/frontend/tests/`
- Integration pack: `web-tool/integration-pack/`
- Deploy: `web-tool/deploy/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project skeleton, toolchain, and references to the existing `gen-val` engine (no upstream modification)

- [ ] T001 Create the `web-tool/` directory tree (`backend/src`, `backend/tests`, `frontend`, `integration-pack`, `deploy`) per plan.md Project Structure
- [ ] T002 Create the ASP.NET Core 8 Web API project `web-tool/backend/src/Acvp.WebTool.Api/Acvp.WebTool.Api.csproj` with nullable reference types enabled, and a solution `web-tool/backend/Acvp.WebTool.sln`
- [ ] T003 Add project references from `Acvp.WebTool.Api.csproj` to the existing `gen-val` Generation/Common libraries (`IGenValInvoker`, request/response models) and the Microsoft Orleans client packages — without modifying any `gen-val/` project
- [ ] T004 [P] Create xUnit test projects `web-tool/backend/tests/Acvp.WebTool.Api.UnitTests/` and `web-tool/backend/tests/Acvp.WebTool.Api.IntegrationTests/` with FluentAssertions and (for integration) `Microsoft.AspNetCore.Mvc.Testing`; add both to the solution
- [ ] T005 [P] Initialize the React 18 + TypeScript + Vite frontend in `web-tool/frontend/` (`package.json`, `vite.config.ts`, `tsconfig.json`) with Vitest + Testing Library configured
- [ ] T006 [P] Configure backend formatting/analyzers (`.editorconfig` + `Directory.Build.props` under `web-tool/backend/`) and frontend ESLint + Prettier in `web-tool/frontend/`

**Checkpoint**: Solutions build empty; `dotnet test web-tool/backend` and `npm test` run with zero tests

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that ALL user stories depend on — engine wiring, job model, artifact storage, DTOs, error mapping, SPA shell

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

### Foundational business-logic tests (MANDATORY — write first, ensure they FAIL) ⚠️

> Constitution Principle V applies to foundational logic too: the job state machine and the artifact-storage privacy boundary are business logic and MUST be specified by a failing test before implementation.

- [ ] T007 [P] Unit test for `ArtifactStore`: per-`jobId` files round-trip, and the **server-only boundary** — `internalProjection.json`/`expectedResults.json` are retrievable only through the server-only read path and are NOT exposed by any client-facing accessor (FR-011, R5, Principle IV) in `web-tool/backend/tests/Acvp.WebTool.Api.UnitTests/ArtifactStoreServerOnlyTests.cs`
- [ ] T008 [P] Unit test for `JobQueue`/`JobStore` state machine: `Queued → Running → (Succeeded | Failed)` only, terminal states immutable, concurrency bounded by `LimitsOptions.MaxConcurrentWork` (data-model.md Job, FR-009) in `web-tool/backend/tests/Acvp.WebTool.Api.UnitTests/JobQueueStateMachineTests.cs`

### Foundational implementation

- [ ] T009 [P] Create strongly-typed options in `web-tool/backend/src/Acvp.WebTool.Api/Options/` (`EngineOptions`, `LimitsOptions` with upload size limit and `MaxConcurrentWork`, `StorageOptions` with artifact root path), bound from `appsettings.json`/environment in `Program.cs`
- [ ] T010 [P] Create request/response DTO records in `web-tool/backend/src/Acvp.WebTool.Api/Models/` (`AlgorithmConfiguration`, `Job`, `Capabilities`, `CheckResult`, `ValidationReport`, `SafeError`) matching `contracts/openapi.yaml` schemas
- [ ] T011 [P] Define the `SafeError` codes and a central exception→`SafeError`/HTTP-status mapping (`INVALID_CONFIGURATION`, `UNSUPPORTED_SELECTION`, `MISSING_FIELD`, `UNKNOWN_TCID`, `MISMATCHED_VECTORSET`, `UPLOAD_TOO_LARGE`, `ENGINE_UNAVAILABLE`, `JOB_NOT_FOUND`, `JOB_NOT_READY`) as middleware in `web-tool/backend/src/Acvp.WebTool.Api/Validation/SafeErrorMiddleware.cs` — never leaking stack traces (FR-008, Principle IV)
- [ ] T012 Implement `ArtifactStore` in `web-tool/backend/src/Acvp.WebTool.Api/Services/ArtifactStore.cs` to satisfy T007 — per-`jobId` filesystem directory for `prompt`/`internalProjection`/`expectedResults`/`validation`/`responses`, with `internalProjection.json` and `expectedResults.json` server-only and never returned to the client (R5, FR-011)
- [ ] T013 Implement the background `JobQueue` + `JobStore` in `web-tool/backend/src/Acvp.WebTool.Api/Services/JobQueue.cs` to satisfy T008 — `Queued → Running → (Succeeded | Failed)` state machine, bounded concurrency from `LimitsOptions`, terminal states immutable (data-model.md Job)
- [ ] T014 Implement `GenValService` skeleton in `web-tool/backend/src/Acvp.WebTool.Api/Services/GenValService.cs` wrapping `IGenValInvoker` (`CheckParameters` / `GenerateAsync` / `ValidateAsync`) and wire the Orleans client + DI in `web-tool/backend/src/Acvp.WebTool.Api/Program.cs` (port existing `AutofacConfig`/Orleans wiring; do not modify grains)
- [ ] T015 Configure the minimal-API host in `Program.cs`: same-origin `/api` route group, static-file serving for the built SPA, options binding, `SafeErrorMiddleware`, and health endpoint
- [ ] T016 [P] Create the frontend app shell in `web-tool/frontend/src/`: routing for the Select → Generate → Upload → Report flow (`pages/`), base layout (`components/`), and a typed API client scaffold in `web-tool/frontend/src/api/` derived from `contracts/openapi.yaml`

**Checkpoint**: Foundation ready — engine reachable via `GenValService`, jobs enqueue with a tested state machine, the artifact privacy boundary is tested, errors map safely, SPA shell renders. User stories can now begin.

---

## Phase 3: User Story 1 - Generate a downloadable test-vector package (Priority: P1) 🎯 MVP

**Goal**: Operator selects algorithm/mode/parameter set in the browser, generates a **prompt package** (`prompt.json` + example `responses.json` + instructions), and downloads it.

**Independent Test**: Select ML-KEM keyGen at ML-KEM-768, click generate, and confirm a well-formed prompt package downloads containing per-case questions, an example response file, and instructions — with no other story implemented.

### Tests for User Story 1 (MANDATORY — write first, ensure they FAIL) ⚠️

- [ ] T017 [P] [US1] Contract test for `GET /api/capabilities` returning the supported ML-KEM/ML-DSA matrix in `web-tool/backend/tests/Acvp.WebTool.Api.IntegrationTests/CapabilitiesContractTests.cs`
- [ ] T018 [P] [US1] Contract test for `POST /api/check` (valid config → 200 CheckResult; unsupported combo → 400 SafeError `UNSUPPORTED_SELECTION`) in `web-tool/backend/tests/Acvp.WebTool.Api.IntegrationTests/CheckContractTests.cs`
- [ ] T019 [P] [US1] Contract test for `POST /api/generate` (→ 202 Job) and `GET /api/jobs/{jobId}` (status polling) in `web-tool/backend/tests/Acvp.WebTool.Api.IntegrationTests/GenerateJobContractTests.cs`
- [ ] T020 [P] [US1] Contract test for `GET /api/jobs/{jobId}/prompt-package`: 200 zip when Succeeded, 409 `JOB_NOT_READY`, 404 `JOB_NOT_FOUND`, **and assert the zip contents include `prompt.json` + a matching example `responses.json` + instructions** (FR-003, FR-004) in `web-tool/backend/tests/Acvp.WebTool.Api.IntegrationTests/PromptPackageContractTests.cs`
- [ ] T021 [P] [US1] Data-privacy integration test (FR-011, SC-007, Principle IV): the prompt package and **no API response** ever exposes `internalProjection.json`/`expectedResults.json`, and the tool never persists IUT source code or private keys (only prompt/response artifacts) in `web-tool/backend/tests/Acvp.WebTool.Api.IntegrationTests/DataPrivacyBoundaryTests.cs`
- [ ] T022 [P] [US1] Unit tests for the configuration validator (accepts in-scope matrix, rejects out-of-scope algorithm/mode/parameterSet combinations, applies ML-DSA safe defaults) in `web-tool/backend/tests/Acvp.WebTool.Api.UnitTests/ConfigurationValidatorTests.cs`
- [ ] T023 [P] [US1] Golden-parity integration test: tool generation for ML-KEM keyGen produces a prompt whose case set matches `GenValAppRunner -g` output for the same registration, in `web-tool/backend/tests/Acvp.WebTool.Api.IntegrationTests/GenerateGoldenParityTests.cs`

### Implementation for User Story 1

- [ ] T024 [P] [US1] Implement the supported-selection matrix + `ConfigurationValidator` in `web-tool/backend/src/Acvp.WebTool.Api/Validation/ConfigurationValidator.cs` (FR-001, FR-002, FR-015; ML-DSA advanced safe defaults)
- [ ] T025 [P] [US1] Implement the `AlgorithmConfiguration → ACVP registration` translation in `web-tool/backend/src/Acvp.WebTool.Api/Services/RegistrationBuilder.cs`
- [ ] T026 [US1] Implement `GET /api/capabilities` endpoint in `web-tool/backend/src/Acvp.WebTool.Api/Endpoints/CapabilitiesEndpoints.cs` (depends on T024)
- [ ] T027 [US1] Implement `POST /api/check` endpoint calling `GenValService.CheckParameters` in `web-tool/backend/src/Acvp.WebTool.Api/Endpoints/CheckEndpoints.cs` (depends on T024, T025)
- [ ] T028 [US1] Implement `POST /api/generate` (enqueue generate job → 202 Job) and the generate job handler that runs `GenerateAsync`, persists `prompt`/`internalProjection`/`expectedResults` via `ArtifactStore`, in `web-tool/backend/src/Acvp.WebTool.Api/Endpoints/GenerateEndpoints.cs` (depends on T013, T014, T025)
- [ ] T029 [US1] Implement `GET /api/jobs/{jobId}` status endpoint in `web-tool/backend/src/Acvp.WebTool.Api/Endpoints/JobsEndpoints.cs` (depends on T013)
- [ ] T030 [US1] Implement prompt-package assembly (zip of `prompt.json` + example `responses.json` + human-readable instructions) in `web-tool/backend/src/Acvp.WebTool.Api/Services/PromptPackageBuilder.cs` and the `GET /api/jobs/{jobId}/prompt-package` download endpoint (FR-003, FR-004; depends on T012, T028)
- [ ] T031 [P] [US1] Implement the selection page (algorithm/mode/parameter set pickers fed by `/capabilities`, blocking unsupported combos) in `web-tool/frontend/src/pages/SelectPage.tsx`
- [ ] T032 [US1] Implement the generate flow with job-status polling and prompt-package download in `web-tool/frontend/src/pages/GeneratePage.tsx` (depends on T031, typed client)
- [ ] T033 [P] [US1] Frontend unit test for the selection/generate flow (unsupported combo disabled; download appears on Succeeded) in `web-tool/frontend/tests/generate.test.tsx`

**Checkpoint**: User Story 1 is fully functional — an operator can generate and download a prompt package for any supported mode, with the data-privacy boundary verified. MVP deliverable.

---

## Phase 4: User Story 2 - Upload responses and view a readable validation report (Priority: P2)

**Goal**: Operator uploads a response file for a prior generate job and receives a per-case pass/fail report, an overall summary, and a downloadable `validation.json` — with verdicts matching the CLI.

**Independent Test**: Using the example response file shipped with a generated prompt package, upload it and confirm a readable pass/fail report whose verdict matches the `GenValAppRunner` workflow.

### Tests for User Story 2 (MANDATORY — write first, ensure they FAIL) ⚠️

- [ ] T034 [P] [US2] Contract test for `POST /api/validate` multipart upload (→ 202 Job; oversize → 413 `UPLOAD_TOO_LARGE`) in `web-tool/backend/tests/Acvp.WebTool.Api.IntegrationTests/ValidateContractTests.cs`
- [ ] T035 [P] [US2] Contract test for `GET /api/jobs/{jobId}/report` and `GET /api/jobs/{jobId}/validation-json` (200 when Succeeded; 409 `JOB_NOT_READY`) in `web-tool/backend/tests/Acvp.WebTool.Api.IntegrationTests/ReportContractTests.cs`
- [ ] T036 [P] [US2] Unit tests for vector-set mismatch detection (response file for a different algorithm/mode/vsId → `MISMATCHED_VECTORSET`, not graded) in `web-tool/backend/tests/Acvp.WebTool.Api.UnitTests/UploadMismatchTests.cs`
- [ ] T037 [P] [US2] Golden-parity integration test across ALL 5 modes (ML-KEM keyGen/encapDecap; ML-DSA keyGen/sigGen/sigVer): API `disposition` equals CLI `validation.json` disposition; corrupting one answer flips both to `failed`, in `web-tool/backend/tests/Acvp.WebTool.Api.IntegrationTests/ValidateGoldenParityTests.cs` (FR-007, SC-003)

### Implementation for User Story 2

- [ ] T038 [P] [US2] Implement structural upload validation + size enforcement + mismatch check in `web-tool/backend/src/Acvp.WebTool.Api/Validation/ResponseUploadValidator.cs` (FR-010, FR-016; depends on T011)
- [ ] T039 [US2] Implement `POST /api/validate` (multipart, persist `responses.json`, enqueue validate job) and the validate job handler running `ValidateAsync` against the stored `internalProjection`/`expectedResults`, in `web-tool/backend/src/Acvp.WebTool.Api/Endpoints/ValidateEndpoints.cs` (depends on T013, T014, T038)
- [ ] T040 [US2] Implement `ValidationReport` assembly (overall disposition, summary counts, per-`tcId` cases) in `web-tool/backend/src/Acvp.WebTool.Api/Services/ValidationReportBuilder.cs` (FR-006)
- [ ] T041 [US2] Implement `GET /api/jobs/{jobId}/report` and `GET /api/jobs/{jobId}/validation-json` endpoints in `web-tool/backend/src/Acvp.WebTool.Api/Endpoints/ReportEndpoints.cs` (depends on T040)
- [ ] T042 [US2] Implement the upload page (select prior generate job, upload responses.json, poll validate job) in `web-tool/frontend/src/pages/UploadPage.tsx`
- [ ] T043 [US2] Implement the report view (overall pass/fail summary, per-case table with failures highlighted, download `validation.json`) in `web-tool/frontend/src/pages/ReportPage.tsx` (FR-006; depends on T042)
- [ ] T044 [P] [US2] Frontend unit test for the report view (failing cases highlighted; validation.json download offered) in `web-tool/frontend/tests/report.test.tsx`

**Checkpoint**: User Stories 1 AND 2 both work independently — full generate → upload → graded report loop with CLI-parity verdicts.

---

## Phase 5: User Story 3 - Company integration support pack (Priority: P3)

**Goal**: Ship per-family sample harnesses (only the module-call line unfilled), field-mapping docs for every mode, example responses, and precise tcId/field-level upload errors that never leak stack traces.

**Independent Test**: Hand the support pack to someone unfamiliar with the project; they produce a valid response for one mode by editing only the module-invocation line; then upload a deliberately malformed file and confirm the tool names the exact offending tcId and field.

### Tests for User Story 3 (MANDATORY — write first, ensure they FAIL) ⚠️

- [ ] T045 [P] [US3] Integration tests for precise upload errors: missing required field → `MISSING_FIELD` with offending `tcId` + `field` + `hint`; unknown tcId → `UNKNOWN_TCID`; malformed JSON → safe error with no stack trace, in `web-tool/backend/tests/Acvp.WebTool.Api.IntegrationTests/UploadErrorTests.cs` (FR-008, SC-004)
- [ ] T046 [P] [US3] Test that each sample harness fills a valid example `responses.json` for its mode and that the field-mapping doc covers every supported mode's input/output fields, in `web-tool/backend/tests/Acvp.WebTool.Api.IntegrationTests/SupportPackTests.cs` (FR-014, SC-006)

### Implementation for User Story 3

- [ ] T047 [US3] Enhance `ResponseUploadValidator` to emit field-level `UploadError` (`tcId` + `field` + `hint`) for missing/extra fields and malformed JSON in `web-tool/backend/src/Acvp.WebTool.Api/Validation/ResponseUploadValidator.cs` (FR-008; extends T038)
- [ ] T048 [P] [US3] Create the ML-KEM sample harness `web-tool/integration-pack/harness_mlkem.py` (read prompt → write responses; only the module-call line to fill) modeled on the demo `iut_mlkem.py`
- [ ] T049 [P] [US3] Create the ML-DSA sample harness `web-tool/integration-pack/harness_mldsa.py` (single module-call line to fill) modeled on the demo `iut_mldsa.py`
- [ ] T050 [P] [US3] Write `web-tool/integration-pack/field-mapping.md` enumerating, per mode (ML-KEM keyGen/encapDecap; ML-DSA keyGen/sigGen/sigVer), each prompt input field and required response output field with encoding (hex/base64) and byte length (FR-014)
- [ ] T051 [P] [US3] Add example `web-tool/integration-pack/sample-responses/` (one `responses.json` per mode) for format reference
- [ ] T052 [US3] Surface field-level upload errors in the upload UI (highlight offending tcId/field with the hint) in `web-tool/frontend/src/pages/UploadPage.tsx` (depends on T042, T047)

**Checkpoint**: All three user stories independently functional; handoff to the company is reduced to one module-call line with exact format guidance and precise upload diagnostics.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Packaging, docs sync, and final verification across all stories

- [ ] T053 [P] Author `web-tool/deploy/Dockerfile.api` (build API + bundle the built SPA) and `web-tool/deploy/docker-compose.yml` (web + existing Orleans Silo, `MaxConcurrentWork` < host CPU) for single-command `docker compose up` (FR-012, FR-013)
- [ ] T054 [P] Add `web-tool/README.md` and keep `contracts/openapi.yaml` + `quickstart.md` in sync with the implemented endpoints (Principle VIII)
- [ ] T055 [P] Add an engine-unavailable resilience test (Orleans unreachable → `ENGINE_UNAVAILABLE` safe error + retry guidance) in `web-tool/backend/tests/Acvp.WebTool.Api.IntegrationTests/EngineUnavailableTests.cs`
- [ ] T056 [P] Add a Playwright smoke test for the Select → Generate → Upload → Report flow in `web-tool/frontend/tests/e2e/smoke.spec.ts` — this is the automated acceptance for **SC-001** (operator completes generate without a CLI) and **SC-002** (operator reads a pass/fail report without inspecting raw files)
- [ ] T057 Verify backend + frontend coverage ≥ 80% floor (Constitution Principle V) and run the `quickstart.md` golden-parity walkthrough end-to-end for all 5 modes; confirm SC-001 (<3 min) and SC-005 (<30 min install) by timing the documented flow

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Setup — **BLOCKS all user stories**. Foundational tests (T007, T008) precede their implementations (T012, T013).
- **User Stories (Phase 3–5)**: All depend on Foundational completion
  - US1 (P1) → US2 (P2) → US3 (P3) in priority order, or in parallel if staffed
- **Polish (Phase 6)**: Depends on the targeted user stories being complete

### User Story Dependencies

- **US1 (P1)**: Depends only on Foundational. Independently testable (generate + download + privacy boundary).
- **US2 (P2)**: Depends on Foundational. Reuses the job/artifact infra; demonstrable with US1's example response file but independently testable via the CLI oracle.
- **US3 (P3)**: Depends on Foundational; its upload-error enhancement (T047/T052) builds on US2's `ResponseUploadValidator` (T038) and upload UI (T042). The deliverable docs/harnesses (T048–T051) are independent.

### Within Each User Story

- Tests (RED) before implementation (GREEN) before refactor
- Validators/builders (models) before endpoints (services) before frontend wiring
- Story complete and independently verifiable before moving to the next priority

### Parallel Opportunities

- Setup: T004, T005, T006 in parallel after T001–T003
- Foundational: T007, T008 (tests) in parallel; T009, T010, T011, T016 in parallel; T012–T015 sequential (shared `Program.cs`/services, gated by their tests)
- US1 tests T017–T023 all parallel; impl T024, T025, T031, T033 parallel
- US2 tests T034–T037 all parallel
- US3 deliverables T048, T049, T050, T051 all parallel
- With staff: US1 (Dev A), US2 (Dev B), US3 (Dev C) proceed concurrently after Phase 2

---

## Branch & PR Mapping (Constitution Governance)

Per the constitution's "one branch (and one PR) per user story" rule, shared work lands first, then each story forks its own short-lived sequential branch:

- **Shared setup + foundational** (Phase 1–2): land on the feature base branch `001-validation-web-tool` before story branches fork from `main`.
- **US1** (Phase 3): branch `002-us1-generate-package` → focused PR.
- **US2** (Phase 4): branch `003-us2-upload-report` → focused PR.
- **US3** (Phase 5): branch `004-us3-support-pack` → focused PR.
- **Polish** (Phase 6): fold into the relevant story PR or a final `005-polish-packaging` branch.

Sync `main` into active branches at least daily; merge back only after tests pass and review approves.

---

## Parallel Example: User Story 1

```bash
# Launch all US1 tests together (write first, expect RED):
Task: "Contract test GET /api/capabilities in CapabilitiesContractTests.cs"
Task: "Contract test POST /api/check in CheckContractTests.cs"
Task: "Contract test POST /api/generate + GET /api/jobs/{id} in GenerateJobContractTests.cs"
Task: "Contract test GET /api/jobs/{id}/prompt-package (incl. contents) in PromptPackageContractTests.cs"
Task: "Data-privacy boundary test in DataPrivacyBoundaryTests.cs"
Task: "Unit tests ConfigurationValidator in ConfigurationValidatorTests.cs"
Task: "Golden-parity generation test in GenerateGoldenParityTests.cs"

# Then launch parallel implementation pieces:
Task: "ConfigurationValidator in Validation/ConfigurationValidator.cs"
Task: "RegistrationBuilder in Services/RegistrationBuilder.cs"
Task: "SelectPage in frontend/src/pages/SelectPage.tsx"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL — blocks all stories; foundational tests first)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: generate + download a prompt package for ML-KEM keyGen, confirm golden parity and the data-privacy boundary
5. Demo the MVP

### Incremental Delivery

1. Setup + Foundational → foundation ready
2. US1 → test independently → demo (MVP: generate package)
3. US2 → test independently → demo (graded report with CLI parity)
4. US3 → test independently → deliver support pack + precise errors
5. Polish → single-command Docker self-host + coverage/quickstart verification

### Parallel Team Strategy

1. Whole team completes Setup + Foundational
2. Then: Dev A → US1, Dev B → US2, Dev C → US3 (coordinating on the shared `ResponseUploadValidator`/`UploadPage` touchpoints between US2 and US3)
3. Stories integrate independently behind the shared job/artifact infrastructure, each on its own branch/PR (see Branch & PR Mapping)

---

## Notes

- [P] tasks = different files, no dependencies on incomplete work
- [Story] label maps each task to its user story for traceability
- Upstream `gen-val/` cryptographic projects are referenced, never modified (Constitution Principle III)
- Server-only artifacts (`internalProjection.json`, `expectedResults.json`) and IUT source/keys are never returned to the client (FR-011, R5) — verified by T007 (unit) and T021 (integration)
- Verify each test fails before implementing; commit after each task or logical group
- Stop at any checkpoint to validate the story independently
