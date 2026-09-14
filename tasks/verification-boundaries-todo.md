# Verification boundaries — implementation tasks

## v1 delivered

- [x] Registry v1: C# production-owner fallbacks plus the first curated focused Data socket
  boundary and test-source mappings.
  - Verify: `./scripts/guard-verification-boundaries.ps1`.
- [x] Positive `VerificationId` selection: `data.item-socket` selects only the socket family.
  - Verify: `dotnet test tests/FusionRpg.Data.Tests/FusionRpg.Data.Tests.csproj -c Release --filter "VerificationId=data.item-socket"`.
- [x] Path planner/runner: explicit added/modified paths, deleted-path support, mandatory session
  scope by default, deterministic owner resolution, additive seam support, and no broad fallback.
  - Verify: `dotnet test tests/FusionRpg.Guard.Tests/FusionRpg.Guard.Tests.csproj -c Release --filter "FullyQualifiedName~VerificationBoundaryWorkflowTests"`.
- [x] Integrity gate: schema/allowlist/path checks, source-owner coverage, duplicate-owner-pattern
  rejection, and zero-match `VerificationId` detection; run by CI and by the runner before use.
  - Verify: `./scripts/guard-verification-boundaries.ps1`.
- [x] Workflow adoption: `test-fast.ps1` requires an explicit project or `-AllDefault`; AGENTS and
  the testing standard make session-scoped `verify-change.ps1` the agent default.

## Follow-on adoption slices

- [ ] Add curated focused groups when a C# migration fallback proves too broad. Each new boundary
  needs a behavior trait and a registry rationale; do not guess from project references.
- [ ] Add a real cross-module seam only when a named contract needs additional proof; fixture-only
  seams are not justification to invent one.
- [ ] Add web/tools/generated/config roots with their own fixed validators. They must not be
  misrepresented as C# `dotnet test` ownership.
- [ ] Integrate `-VerificationPaths` into `deploy-play.ps1` after reconciling its concurrently owned
  deployment regions.

## v1 proof record

```powershell
# Representative focused production change (two guards + eight socket tests):
.\scripts\verify-change.ps1 -Paths src/FusionRpg.Data/Sqlite/RpgStore.Sockets.cs -AllowUnscoped

# Normal agent invocation (session is required):
.\scripts\verify-change.ps1 -Paths tests/FusionRpg.Guard.Tests/VerificationBoundaryWorkflowTests.cs `
  -Session verification-boundaries-20260913-6f31
```

CI/nightly/release remain the unfiltered full-evidence owners; neither command authorizes a broad
suite after a failure or an unmapped path.
