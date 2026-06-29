# Quickstart: FIPS 203/204 Validation Web Tool

This is the developer/operator quickstart for the planned web tool. Until the tool is
implemented, the underlying flow can be exercised with the existing CLI demo (see
`fips-203-204-demo/README_DEMO_PQC-zh-TW.md`).

## Prerequisites

- .NET 8 SDK
- Node.js (LTS) + npm — for the React/Vite frontend
- Docker + Docker Compose — for the packaged self-host deployment
- The repository's existing setup (symlink `_config/Directory.*.props` to root if needed;
  `gen-val/samples/sharedappsettings.json` with `MaxConcurrentWork` < CPU count)

## Self-host (target operator flow)

```bash
cd web-tool/deploy
docker compose up
# Open the printed URL in a browser (web + Orleans Silo both start)
```

Operator steps in the browser:

1. Choose algorithm (ML-KEM / ML-DSA) and mode.
2. Pick parameter set(s); advanced options use safe defaults.
3. Click **Generate test vectors** → wait for the job → download the prompt package
   (`prompt.json` + example `responses.json` + instructions).
4. Hand `prompt.json` to your engineer; they produce `responses.json` with your module
   (fill the single module-call line in the provided sample harness).
5. Upload `responses.json` → wait for the job → read the pass/fail report; optionally
   download `validation.json`.

## Local development

```bash
# Terminal 1 — Orleans Silo (existing, unmodified)
cd gen-val/samples/NIST.CVP.ACVTS.Orleans.ServerHost
dotnet run --console            # dashboard on :8081

# Terminal 2 — Web API
cd web-tool/backend/src/Acvp.WebTool.Api
dotnet run

# Terminal 3 — Frontend
cd web-tool/frontend
npm install && npm run dev
```

## Tests (TDD — write first)

```bash
# Backend unit + integration (xUnit)
dotnet test web-tool/backend

# Golden parity: tool disposition must equal GenValAppRunner CLI for the same inputs
#   (covers all 5 modes; see Acvp.WebTool.Api.IntegrationTests)

# Frontend
cd web-tool/frontend && npm test
```

## Verifying correctness (golden parity)

For each of the 5 modes (ML-KEM keyGen/encapDecap; ML-DSA keyGen/sigGen/sigVer):

1. Generate via the API and via `GenValAppRunner -g registration_*.json`.
2. Produce responses (demo harness or sample harness).
3. Validate via the API and via `GenValAppRunner -n internalProjection.json -b responses.json`.
4. Assert the API's `disposition` equals the CLI's `validation.json` disposition.
5. Corrupt one answer and confirm both flip to `failed`.

## Configuration

- Engine/Orleans + limits configured via `appsettings*.json` / environment (no hardcoded
  secrets). Keep `MaxConcurrentWork` below the host CPU count or generation/validation fails.
- Upload size limit and artifact storage path are configurable via the Options pattern.
