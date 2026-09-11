# Implementation Plan: live setup panel skip

**Program:** `live-setup-skip`  
**Capability map:** [live-setup-skip-map.md](../docs/architecture/live-setup-skip-map.md)  
**Spec:** [spec-setup-skip.md](../docs/architecture/live-setup-skip/spec-setup-skip.md)

## Overview

Implement one developer-only command that invokes the vanilla plant-selection completion path from
the injector, exposes it through the existing debug API, and proves it in a real MelonLoader run.

## Architecture decisions

- Use `InitBoard.QuickInGame()` as the default method because its name indicates the host game's
  intended quick path.
- Keep `InGameUI.OnStartBattleButtonClick()` as an explicit probe method so LIVE testing can choose
  the exact button handler without automatically invoking two transitions.
- Run only from `CheatCommandRunner.Drain`; do not add a Harmony patch to the main loop or hide UI
  objects.
- Keep the feature behind `FUSIONRPG_SETUP_SKIP=1` or `DEBUG-SETUP-SKIP`.

## Task list

### Phase 1: Injector vertical slice

- [x] Task 1: Add `DebugActions.SkipSetup` and `debug.skip-setup` dispatch.
  - Acceptance: gate, method validation, null checks, event acknowledgement, and exception safety.
  - Verify: focused injector tests and host build.
  - Files: `src/FusionRpg.Injector/DebugActions.cs`, `src/FusionRpg.Injector/CheatCommandRunner.cs`, `tests/FusionRpg.Injector.Tests/SetupSkipTests.cs`.

### Checkpoint: Injector

- [x] Both method paths compile against the configured 3.9 MelonLoader interop.
- [x] Disabled, invalid, and accepted command behavior is covered by the gated implementation and
  server contract tests; direct IL2CPP object unit tests require the game host.

### Phase 2: Server contract

- [x] Task 2: Add `/api/debug/setup/skip` with acknowledgement polling.
  - Acceptance: validates method, refuses disconnected injector, returns honest timeout, and
    returns the injector acknowledgement on success.
  - Verify: `LawnQuickStartEndpointTests` plus new endpoint tests.
  - Files: `src/FusionRpg.Server/DebugEndpoints.cs`, `tests/FusionRpg.Server.Tests/SetupSkipEndpointTests.cs`.

### Checkpoint: Contract

- [x] Server tests prove enqueue is not treated as completion.
- [x] Focused existing server debug tests remain green.

### Phase 3: LIVE proof and runbook

- [x] Task 3: Build, deploy, and prove the default candidate on the target game profile.
  - Acceptance: no manual setup click; endpoint acknowledgement is correlated; board progress is
    observed; result names the winning method and profile. The direct `button` comparison remains
    follow-up work because `quick` succeeded.
  - Verify: MelonLoader 3.9 build, live endpoint call, event polling, and smoke script. BepInEx
    build is deferred because the target install has no BepInEx interop.
  - Files: `docs/runbook/live-test-ssot.md`, `scripts/prove-live-setup-skip.ps1`.

### Checkpoint: Complete

- [x] Focused tests pass.
- [x] Available MelonLoader host builds succeed; BepInEx is blocked by the absent host pack.
- [x] LIVE proof is recorded.
- [x] No game binary or vendor file changed.

## Risks and mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| `QuickInGame` is metadata-visible but unsuitable in the current state | High | Probe it explicitly, then probe the exact button handler; wait for acknowledgement and board progress |
| Calling while initialization is incomplete | High | Null/readiness checks, main-thread drain, no automatic retries |
| 3.8.1 and 3.9 wrappers diverge | High | Compile each host against its matching interop and record profile-specific proof |
| UI is hidden but coroutine remains blocked | High | Never mutate panel visibility; invoke vanilla transition only |

## Open questions

- LIVE validation decides whether `quick` or `button` becomes the documented default for each game
  profile. Until then, `quick` remains the default and `button` remains explicit.
