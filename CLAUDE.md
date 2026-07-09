<!-- SPECKIT START -->
For additional context about technologies to be used, project structure,
shell commands, and other important information, read the current plan:
`specs/001-validation-web-tool/plan.md`

Feature status: the FIPS 203/204 Validation Web Tool is **implemented and merged to
`master`** (PRs #3–#7, consolidated by PR #8 on 2026-07-09; all feature branches
deleted). Task-level record: `specs/001-validation-web-tool/tasks.md` (56/57 done;
T056 Playwright smoke deferred).

Stack: C# / .NET 8 (ASP.NET Core 8 Minimal API) reusing `gen-val` `IGenValInvoker` +
the existing Orleans Silo; React 18 + Vite + TypeScript SPA; Docker Compose (Silo +
API in one container, localhost clustering). Code lives under `web-tool/`.

Repo note: since PR #8 this is a **slim fork** — `gen-val/` is pruned to ML-KEM /
ML-DSA and their SHA/SHAKE dependencies (engine behavior unchanged). Do not assume
other upstream algorithms or test projects still exist.

Supporting docs: `spec.md`, `research.md`, `data-model.md`, `contracts/openapi.yaml`,
`quickstart.md`, `tasks.md` under `specs/001-validation-web-tool/`.
<!-- SPECKIT END -->
