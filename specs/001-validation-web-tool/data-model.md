# Phase 1 Data Model: FIPS 203/204 Validation Web Tool

Entities are derived from the spec's Key Entities and the existing ACVP artifact shapes
(`prompt.json`, `internalProjection.json`, `expectedResults.json`, `responses.json`,
`validation.json`) observed in `fips-203-204-demo/`.

## AlgorithmConfiguration

The user's selection that defines a generation request.

| Field | Type | Notes / Validation |
|-------|------|--------------------|
| `algorithm` | enum `ML-KEM` \| `ML-DSA` | Required; in scope only |
| `mode` | enum | ML-KEM: `keyGen`, `encapDecap`; ML-DSA: `keyGen`, `sigGen`, `sigVer`. Must be valid for `algorithm` |
| `parameterSets` | string[] | ML-KEM: subset of `ML-KEM-512/768/1024`; ML-DSA: subset of `ML-DSA-44/65/87`; non-empty |
| `advancedOptions` | object (optional) | ML-DSA only; safe defaults applied when omitted (deterministic/externalMu/preHash/hashAlgs). MVP hides these |

**Rules**: Reject any combination outside the supported matrix (FR-001, FR-002, FR-015).
The configuration is translated into an ACVP `registration` for `CheckParameters` /
`GenerateAsync`.

## Job

A unit of background work (generation or validation).

| Field | Type | Notes |
|-------|------|-------|
| `jobId` | string (GUID) | Primary identifier |
| `vsId` | long | Vector-set id passed to `GenerateAsync` / `ValidateAsync` |
| `kind` | enum `generate` \| `validate` | — |
| `status` | enum `Queued` \| `Running` \| `Succeeded` \| `Failed` | State machine below |
| `configuration` | AlgorithmConfiguration | Set for `generate` jobs |
| `createdAt` / `completedAt` | timestamp | — |
| `error` | SafeError? | Present when `status = Failed`; no stack traces |

**State transitions**: `Queued → Running → (Succeeded | Failed)`. Terminal states are
immutable. Polling reads status; results are retrievable only in `Succeeded`.

## TestVectorPrompt

The generated questions for a configuration (the downloadable package contents).

| Field | Type | Notes |
|-------|------|-------|
| `jobId` | string | Owning generate job |
| `vsId` | long | Matches the prompt JSON |
| `algorithm` / `mode` | string | Echoed from configuration |
| `promptJson` | file | The ACVP `prompt.json` (per-case questions, each with `tcId`) |
| `exampleResponsesJson` | file | A matching example `responses.json` for format reference |
| `instructions` | file/text | Human-readable how-to-produce-responses |

**Server-only artifacts (never sent to client)**: `internalProjection.json`,
`expectedResults.json` — retained server-side, used only for validation (FR-011/R5).

## ResponseSet

The IUT answers uploaded for grading.

| Field | Type | Notes / Validation |
|-------|------|--------------------|
| `jobId` | string | Must reference an existing generate job |
| `vsId` | long | Must match the prompt's `vsId` (else mismatch error, FR-010) |
| `algorithm` / `mode` | string | Must match the prompt (else mismatch error) |
| `testGroups[].tests[]` | array | Each test keyed by `tcId`; required output fields per mode present |

**Rules**: Structural validation before engine call; missing/extra `tcId` or required field
→ precise `UploadError` (FR-008). Bounded upload size (FR-016).

## ValidationReport

The graded outcome shown to the operator.

| Field | Type | Notes |
|-------|------|-------|
| `jobId` | string | Owning validate job |
| `disposition` | enum `passed` \| `failed` | Overall verdict (matches CLI, FR-007) |
| `summary` | object | Counts: total / passed / failed |
| `cases[]` | array | Per-`tcId` pass/fail (+ reason on fail) |
| `validationJson` | file | Downloadable machine-readable `validation.json` |

## UploadError / SafeError

| Field | Type | Notes |
|-------|------|-------|
| `code` | string | Stable machine code (e.g. `MISSING_FIELD`, `UNKNOWN_TCID`, `MISMATCHED_VECTORSET`, `ENGINE_UNAVAILABLE`) |
| `message` | string | Safe, human-readable; no stack traces (FR-008, Principle IV) |
| `tcId` | int? | Offending test case when applicable |
| `field` | string? | Offending field when applicable |
| `hint` | string? | Correction suggestion |

## IntegrationSupportPack (deliverable, not runtime state)

Sample harnesses (`harness_mlkem`, `harness_mldsa`), field-mapping documentation
(per-mode input/output fields, encoding hex/base64, byte length), and example
`responses.json` per mode. Tracked as repository artifacts under `web-tool/integration-pack/`.
