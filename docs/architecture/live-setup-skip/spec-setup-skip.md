# Spec: live setup panel skip

**Program:** `live-setup-skip`  
**Module:** `setup-skip`  
**Status:** Implemented; `quick` LIVE-proven on `pvzrh-3.9` 2026-09-09. The explicit `button`
probe remains available for profile comparison.

## Objective

Add a developer-only command that completes the vanilla PVZ plant-selection setup panel so live
tests do not require an operator to click the “Let's Rock” button. The command must use the game's
own IL2CPP-exposed methods, execute on Unity's main thread, and report an observed acknowledgement
instead of claiming that a queued command succeeded.

The first path is `InitBoard.QuickInGame()`. A second explicit probe path calls
`InGameUI.OnStartBattleButtonClick()` so the two local 3.9 candidates can be tested independently.
The implementation must not hide UI objects while leaving the game's selection coroutine waiting.

## Grounding and constraints

- The injector already routes commands through `CheatCommandRunner.Drain` from `InjectorLoop` on
  the Unity thread ([InjectorLoop.cs](../../../src/FusionRpg.Injector/Host/InjectorLoop.cs:52)).
- Level entry already uses vanilla `UIMgr.EnterGame` ([DebugActions.cs](../../../src/FusionRpg.Injector/DebugActions.cs:1225)).
- The existing live harness uses command acknowledgement events and does not treat HTTP enqueue as
  completion ([live-test-ssot.md](../../runbook/live-test-ssot.md:29)).
- The injector spec forbids patching `GameAPP.Start`, `Update`, and `EventNodes`
  ([injector/spec.md](../../injector/spec.md:62)).
- The local 3.9 assembly exposes `InitBoard.QuickInGame`, `InitBoard.PreSelect`, and
  `InGameUI.OnStartBattleButtonClick` in its read-only IL2CPP metadata. The local 3.9 pack is
  `H:\Games\PVZ-Fusion-3.9_MelonLoader\MelonLoader\Il2CppAssemblies\Assembly-CSharp.dll`.

## Contract

### Injector command

Command name: `debug.skip-setup`.

Payload:

```json
{ "method": "quick" }
```

`method` is `quick` by default and may be `button` for the explicit handler probe. Unknown methods
are refused. The command is accepted only when `FUSIONRPG_SETUP_SKIP=1` or the existing debug
toggle `DEBUG-SETUP-SKIP` is active.

The injector emits exactly one `debug.setup.skip` event for each accepted command:

```json
{
  "ok": true,
  "method": "quick",
  "ready": true,
  "board": true,
  "ui": true
}
```

Failures emit the same event with `ok: false` and a short `error`; they never throw through the
main loop. Repeated calls are allowed because the vanilla handlers are responsible for their own
state transition; no automatic double-call fallback is performed.

### Server endpoint

`POST /api/debug/setup/skip` forwards the command and waits for a later `debug.setup.skip` event.
It returns conflict when the injector is disconnected, when the injector refuses the gate, or when
the acknowledgement does not arrive before the requested timeout. A successful response includes
`ok`, `method`, and the acknowledgement payload.

Request body:

```json
{ "method": "quick", "timeoutSec": 15 }
```

The endpoint accepts only `quick` and `button`. `timeoutSec` is a structural polling bound, not a
balance value.

## Project structure

- `src/FusionRpg.Injector/DebugActions.cs` — Unity-side setup handler invocation.
- `src/FusionRpg.Injector/CheatCommandRunner.cs` — command dispatch.
- `src/FusionRpg.Server/DebugEndpoints.cs` — HTTP forwarding and acknowledgement polling.
- `tests/FusionRpg.Injector.Tests/` — gate and dispatch tests where host references permit.
- `tests/FusionRpg.Server.Tests/LawnQuickStartEndpointTests.cs` — endpoint refusal, timeout, and
  acknowledgement tests.
- `docs/runbook/live-test-ssot.md` — operator command and proof record.

## Code style

Keep the Unity boundary narrow and explicit:

```csharp
var method = Str(p, "method") ?? "quick";
if (method == "quick") InitBoard.Instance.QuickInGame();
else if (method == "button") InGameUI.Instance.OnStartBattleButtonClick();
else return Refuse("unknown setup method");
```

The real implementation must null-check instances, catch boundary exceptions, and emit an event.
It must not mutate panel GameObjects, write game memory by offset, or add a new native bridge.

## Testing strategy

- Unit/host tests: accepted methods, unknown-method refusal, disabled-gate refusal, null-instance
  refusal, and exception-to-event conversion.
- Server integration tests: disconnected injector refusal and honest timeout when no injector ack
  arrives. These use the existing in-process `MapDebug` harness.
- Build proof: compile the MelonLoader host against the local 3.9 assembly. BepInEx compilation is
  deferred until a BepInEx game pack supplies `BepInEx\core` and `BepInEx\interop`; the current
  target install is MelonLoader-only.
- LIVE proof: deploy the built MelonLoader host, call `POST /api/debug/setup/skip` with `quick`,
  and verify `debug.setup.skip ok=true` followed by observable board progress. The exact vanilla
  method behavior is not considered proven by metadata or compilation alone. The 2026-09-09 proof
  used the endpoint acknowledgement plus subsequent `board.start`/scenario entity events; the
  setup event acknowledgement did not expose a stable persisted event id in the API response.

## Boundaries

- Always: keep the feature debug-only; run on Unity main thread; use vanilla handlers; emit an
  acknowledgement; preserve both BepInEx and MelonLoader host separation; run focused tests after
  each slice.
- Ask first: widening this into player-facing menu automation, adding a new persistent config
  surface, or changing the level-entry safety gate.
- Never: patch the PVZ game binary; use native memory offsets; hide the panel without completing
  its state machine; patch `GameAPP.Start`/`Update`/`EventNodes`; load BepInEx and MelonLoader
  together.

## Success criteria

1. A disabled injector refuses `debug.skip-setup` without calling a game method.
2. `quick` and `button` each reach the correct vanilla method on the Unity main thread and emit a
   structured acknowledgement.
3. The server endpoint reports the actual acknowledgement or an honest timeout.
4. The available MelonLoader host builds succeed with its matching 3.9 interop references; a
   BepInEx build requires a separate BepInEx game pack.
5. A controlled LIVE run proves the setup panel advances without a manual click, and the proof is
   recorded with method, game profile, observable event ids, and result.

## Open questions

- Which method is the most stable 3.9 path remains a LIVE question: `QuickInGame` is the preferred
  default; the button handler is an explicit fallback probe, not an automatic double invocation.
- 3.8.1 and 3.9 must both be tested before making either method the default for all profiles.

## Design-gate checklist

- [x] Subsystems identified: injector command drain, server debug endpoint, live-test runbook.
- [x] Required architecture, injector, level-entry, overlay-loop, and live-test documents read.
- [x] `decisions.md` checked; no existing lock requires a new architecture decision for this
  developer-only command.
- [x] Existing command and endpoint patterns verified against code.
- [x] Focused existing quick-start endpoint tests run: 5/5 passed.
- [x] No actor combat/derived magnitude is produced or consumed; ActorHub gate is not applicable.
- [x] `InitBoard.QuickInGame()` is LIVE-proven on `pvzrh-3.9`; the `button` path remains an
  explicit comparison probe.
