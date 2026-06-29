<!-- SPECKIT START -->
For additional context about technologies to be used, project structure,
shell commands, and other important information, read the current plan:
`specs/001-validation-web-tool/plan.md`

Active feature: FIPS 203/204 Validation Web Tool (branch `001-validation-web-tool`).
Stack: C# / .NET 8 (ASP.NET Core 8 Web API) reusing `gen-val` `IGenValInvoker` + the
existing Orleans Silo (unmodified); React + Vite + TypeScript SPA; Docker Compose.
Supporting docs: `spec.md`, `research.md`, `data-model.md`, `contracts/openapi.yaml`,
`quickstart.md` under `specs/001-validation-web-tool/`.
<!-- SPECKIT END -->
