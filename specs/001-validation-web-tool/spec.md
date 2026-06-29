# Feature Specification: FIPS 203/204 Validation Web Tool

**Feature Branch**: `001-validation-web-tool`

**Created**: 2026-06-29

**Status**: Draft

**Input**: User description: "參考 ~/project-docs/planning 內的檔案" — a self-hosted web tool that lets non-cryptographer users validate their cryptographic module (IUT) implementations of FIPS 203 (ML-KEM) and FIPS 204 (ML-DSA) without using a command line, reusing the existing NIST ACVP Gen/Val engine.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Generate a downloadable test-vector package (Priority: P1)

A non-technical operator at the partner company opens the tool in a browser, picks a
cryptographic algorithm and mode (e.g. ML-KEM keyGen), selects a parameter set, and
generates a test-vector "prompt" package they can download. The package contains the
questions their module must answer, an example answer file, and plain-language
instructions.

**Why this priority**: This is the entry point of the whole validation workflow and the
smallest slice that delivers standalone value — without a generated prompt, nothing
downstream can happen. It proves the tool can drive the underlying validation engine
end-to-end for at least one algorithm.

**Independent Test**: Select ML-KEM keyGen at a given parameter set, click generate, and
confirm a well-formed prompt package downloads, containing the test cases, an example
response file, and instructions — verifiable without any other story implemented.

**Acceptance Scenarios**:

1. **Given** the tool is running and the operator is on the selection screen, **When** they
   choose ML-KEM, mode keyGen, parameter set ML-KEM-768 and click "Generate test vectors",
   **Then** the tool produces a prompt package for that exact configuration and offers it for
   download.
2. **Given** a generated prompt package, **When** the operator opens it, **Then** it includes
   the per-test-case questions, a matching example response file, and human-readable
   instructions for producing real responses.
3. **Given** an operator selects an unsupported algorithm/mode/parameter combination, **When**
   they attempt to generate, **Then** the tool prevents it and explains which selections are
   supported.

---

### User Story 2 - Upload responses and view a readable validation report (Priority: P2)

After the company's engineer has run the prompt through their own module and produced a
response file, the operator uploads that file and receives a human-readable report showing
which test cases passed or failed, an overall summary, and a downloadable machine-readable
result.

**Why this priority**: This is the payoff of the workflow — the actual "grading". It depends
on a prompt existing (US1) but delivers the core business value: telling the company whether
their implementation is correct. It can be demonstrated independently using the example
response file shipped with the prompt.

**Independent Test**: Using the example response file from a generated prompt, upload it and
confirm the tool returns a readable pass/fail report whose verdict matches the reference
command-line workflow.

**Acceptance Scenarios**:

1. **Given** a generated prompt and a matching response file, **When** the operator uploads the
   response file, **Then** the tool validates it and shows a per-test-case pass/fail report
   plus an overall summary.
2. **Given** a completed validation, **When** the operator chooses to export, **Then** they can
   download the machine-readable validation result.
3. **Given** a response file containing one or more incorrect answers, **When** it is validated,
   **Then** the affected test cases are clearly marked as failed and the failing items are
   highlighted in the report.
4. **Given** the same prompt and response inputs, **When** validated through the tool and
   through the reference command-line workflow, **Then** the pass/fail verdicts are identical.

---

### User Story 3 - Company integration support pack (Priority: P3)

So the company's engineer can produce the response file with minimal effort and few format
mistakes, the tool/deliverable provides example response-producing harnesses (one per
algorithm family), field-mapping documentation for every supported mode, and precise,
actionable error messages when an uploaded file is malformed.

**Why this priority**: The riskiest part of the project is the handoff step that happens on the
company's machines, outside the tool. Reducing that step to "fill in one line" and giving
exact format guidance and upload errors de-risks adoption. It is valuable on its own (it can
be delivered and reviewed as documentation + samples) but is lower priority than producing and
grading vectors.

**Independent Test**: Hand the support pack to someone unfamiliar with the project and confirm
they can produce a valid response file for one mode by editing only the module-invocation
point of the sample harness; then upload a deliberately malformed file and confirm the tool
names the exact offending test case and field.

**Acceptance Scenarios**:

1. **Given** the support pack, **When** an engineer opens the sample harness for ML-KEM (or
   ML-DSA), **Then** the read-prompt / write-response scaffold is complete and only the
   module-invocation step remains to be filled in.
2. **Given** the field-mapping documentation, **When** an engineer looks up a mode, **Then**
   each prompt input field and required response output field is listed with its encoding
   (e.g. hex/base64) and byte length.
3. **Given** an uploaded response file missing a required field, **When** it is validated,
   **Then** the tool reports the specific test case (by tcId) and field at fault with a
   correction hint, and does not expose an internal stack trace.

---

### Edge Cases

- **Long-running jobs**: Large generation/validation runs (e.g. ML-DSA sigGen with many cases)
  must not force the operator to hold an open, blocking request; progress is trackable and
  results are retrievable when ready.
- **Large files**: Prompt/response files may be large; upload/download must handle them within
  a defined size limit and reject oversized files with a clear message.
- **Mismatched file**: An uploaded response file that belongs to a different
  algorithm/mode/configuration than the prompt is detected and reported rather than silently
  mis-graded.
- **Malformed / partial upload**: Invalid JSON or missing fields produce precise,
  test-case-level errors, never raw stack traces.
- **Concurrent jobs**: Multiple generation/validation requests are handled within a bounded
  capacity without exhausting the host machine.
- **Engine/back-end unavailable**: If the underlying validation engine is not reachable, the
  operator sees a clear, non-sensitive error and can retry.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST let a user select a supported algorithm and mode through a graphical
  interface without using a command line — ML-KEM (keyGen, encapDecap) and ML-DSA (keyGen,
  sigGen, sigVer).
- **FR-002**: System MUST let the user choose supported parameter sets (ML-KEM-512/768/1024;
  ML-DSA-44/65/87) and MUST supply safe defaults for advanced options.
- **FR-003**: System MUST generate a test-vector prompt for the selected configuration and let
  the user download it as a file.
- **FR-004**: When delivering a prompt, System MUST also provide a matching example response
  file and human-readable instructions describing how to produce real responses.
- **FR-005**: System MUST accept an uploaded response file produced externally by the user's
  module and validate it against the originally generated test vectors for that configuration.
- **FR-006**: System MUST present validation results as a human-readable report showing
  per-test-case pass/fail, an overall summary, and an option to download the machine-readable
  validation result.
- **FR-007**: Validation verdicts produced by the tool MUST match the verdicts produced by the
  reference command-line workflow for identical inputs (golden parity).
- **FR-008**: System MUST validate uploaded files and, on malformed or incomplete input, report
  precise, actionable errors identifying the offending test case (by tcId) and field, without
  exposing internal stack traces.
- **FR-009**: System MUST handle long-running generation/validation without requiring the user
  to keep a blocking request open; the user MUST be able to track progress and retrieve results
  when ready.
- **FR-010**: System MUST detect when an uploaded response file does not correspond to the
  prompt's algorithm/mode/configuration and MUST report the mismatch instead of grading it.
- **FR-011**: System MUST never request, store, or transmit the user's module source code or
  private keys; only prompt and response artifacts are exchanged.
- **FR-012**: System MUST be deployable and fully operable within the user's own/internal
  environment with no external service dependency at runtime, so that test data never leaves
  that environment.
- **FR-013**: System MUST be startable by an operator with a single, documented startup action
  (no per-component manual wiring).
- **FR-014**: System MUST provide an integration support pack containing (a) example
  response-producing harnesses for ML-KEM and ML-DSA whose only unfilled step is the
  module-invocation call, and (b) field-mapping documentation enumerating each mode's prompt
  input fields and required response output fields with their encoding and byte length.
- **FR-015**: System MUST reject or clearly flag selections outside the supported scope (only
  the listed FIPS 203/204 modes and parameter sets).
- **FR-016**: System MUST enforce an upload size limit and reject oversized files with a clear
  message.

### Key Entities *(include if feature involves data)*

- **Algorithm Configuration**: The user's chosen algorithm, mode, parameter set(s), and
  advanced options that define a generation request.
- **Test Vector Prompt**: The generated set of per-test-case questions for a configuration;
  each case is identified by a test-case id (tcId). Shipped together with an example response
  file and instructions.
- **Response Set**: The answers produced by the user's module, keyed by tcId, uploaded for
  grading.
- **Validation Report**: The graded outcome — per-test-case disposition (pass/fail), an overall
  summary, and a downloadable machine-readable result.
- **Integration Support Pack**: The sample harnesses, field-mapping documentation, and example
  response file delivered to help the company produce valid responses.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A non-technical operator can produce and download a test-vector package for any
  supported algorithm/mode in under 3 minutes without using a command line.
- **SC-002**: A non-technical operator can upload a response file and view a readable pass/fail
  report without manually inspecting any raw file.
- **SC-003**: For all five supported algorithm-mode combinations (ML-KEM keyGen, ML-KEM
  encapDecap, ML-DSA keyGen, ML-DSA sigGen, ML-DSA sigVer), the tool's pass/fail verdict matches
  the reference command-line verdict in 100% of golden comparisons.
- **SC-004**: 100% of malformed-upload test cases surface an error that names the specific test
  case and field at fault, with no raw stack trace shown to the user.
- **SC-005**: A new operator can install and start the tool in their own environment, following
  the provided documentation, in under 30 minutes.
- **SC-006**: The company engineer can produce a valid response file by editing only the
  module-invocation portion of the provided sample harness (a single integration point).
- **SC-007**: No module source code or private key material is transmitted to or stored by the
  tool — only prompt and response artifacts are exchanged.

## Assumptions

- The reference validation engine already present in this repository supports all listed
  FIPS 203/204 modes through a programmatic interface, and the tool reuses it rather than
  reimplementing any cryptography.
- The user's cryptographic module (IUT) runs on the user's side; producing the response file
  inherently requires the company's own engineer for one module-invocation step, which cannot
  be eliminated by any delivery method.
- Advanced ML-DSA options (e.g. deterministic, externalMu, preHash, hash algorithm selection)
  have safe defaults and may be hidden from non-expert users.
- Scope is frozen for this term to the listed FIPS 203/204 modes and parameter sets; other
  algorithms, cloud multi-tenancy, and an account/permission system are out of scope.
- A persistent history of past runs may optionally be offered but is not required for the MVP.
- Operators run the tool on infrastructure they control (their own machine or internal
  network); internet access is not assumed at runtime.
