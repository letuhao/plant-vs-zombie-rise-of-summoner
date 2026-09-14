# Spec: `control-click`

**Program:** `game-control` · **Map:** [../game-control-map.md](../game-control-map.md)
**Ideal:** [../game-control-ideal.md](../game-control-ideal.md) · **Standard:**
[../../contributing/live-probe-standard.md](../../contributing/live-probe-standard.md)
**Depends on:** `control-inspect` (refs + snapshots)

---

## Objective

Act on a ref from the current `inspect` snapshot — Playwright's `browser_click`. Resolve
the ref to a live object (re-scan, match native ptr, confirm runtime type **and position**:
native ptrs are reused after death — `match-runtime.md` — so ptr+type alone can resolve to
a *different* entity than inspected), then invoke the type's known method: `CardUI` →
`Mouse.ClickOnCard`, lawn cell → set `Mouse.Instance` cell fields + `TryToSet*`, menus →
the `UIMgr` method table. A ref from a stale snapshot, a reused ptr, or a moved entity is
refused with an error naming the fresh snapshot — never a blind retry.

Who uses this: `control-act` (verb chains), any operator driving menus/cards by ref,
and text-anchored clicks: `debug.evaluate-text` matches engine `Text`/`TextMeshProUGUI`
content directly (no OCR) and resolves the nearest clickable ancestor (first
ancestor-or-self with a `*Btn*` component, else a 2D-collider hitbox, else bare paint
with the nearby tree). Proven live 2026-09-14: text "确定" → `PauseMenu_Btn` ptr →
`OnMouseDown`+`OnMouseUp` via engine dispatch → screenshot proved the update dialog
dismissed.

Success: click-by-ref on a live menu navigates; click-by-ref on a lawn card selects it;
a stale ref fails loudly with the recovery instruction; every response bundles the
acted-upon scope snapshot (richer-default contract).

**ASSUMPTIONS I'M MAKING:**

1. Callee set is closed and already invoked somewhere in our code (`ClickOnCard`,
   `TryToSetPlantByCard/ZombieByCard`, `DisassemblePlant`, `TryToSetPlantByGlove`,
   `CardUI.UseOnce`, `Button.onClick`, the ~20 `UIMgr` methods).
2. Ref table lives injector-side, keyed per snapshot id, entries hold `(ptr, typeName)`.
3. This module never moves the OS cursor (that is `control-cursor`).

→ Correct me now or implementation proceeds with these.

## Tech stack

- Injector C# net6, main-thread drain only (all callee invocations run on the drain —
  mid-hook invocation is out of order per record-then-drain).
- Server net8 relay (`DebugEndpoints.cs`, scope banners).
- No new packages, no new Unity references.

## Commands

```powershell
.\scripts\guard-debug-scope.ps1
dotnet test tests/FusionRpg.Guard.Tests --filter "FullyQualifiedName~DebugScope"
curl -X POST http://127.0.0.1:5088/api/debug/click -H "Content-Type: application/json" -d '{"ref":"c3"}'
```

## Project structure

| Path | Duty |
|---|---|
| `src/FusionRpg.Injector/ControlRefs.cs` (new) | Snapshot-scoped ref table: ` snapshotId → [(ref, ptr, typeName, row, col)]` (snapshot id = the `debug.inspect` event id); resolve+reverify (`FindObjectsOfType`, ptr match, type confirm, position confirm); stale/reused/moved → descriptive refusal |
| `src/FusionRpg.Injector/ControlClick.cs` (new) | Type-dispatched invoke; emits `debug.click.done` with `{ref, action, ok, snapshot}` |
| `src/FusionRpg.Injector/CheatCommandRunner.cs` | One `debug.click` case (`{ref}` payload) |
| `src/FusionRpg.Server/DebugEndpoints.cs` | `POST /api/debug/click` (Game Injector Debug banner + relay) |

## Code Style

Re-verify, then act, then prove — each step fallible with a named error. Invocation
uses two paths (proven live 2026-09-14): direct `MethodInfo.Invoke` first (preserves
return values); on mirror-mismatch failure, engine `SendMessage` dispatch by name for
0-arg calls (no `Type` needed). The emit records `via: invoke|sendmessage`. Unity-message
handlers (`OnMouseDown/Up` on the dialog's `PauseMenu_Btn`) go through `SendMessage` —
dismissing the live update popup that way is this module's standing proof. Type names
come from the engine's own `Object.ToString()`, never `GetType().Name` (which reports
the interop base on unhollowed objects — measured live: 228× "Component").

```csharp
public static bool Click(string snapshotId, string refId)
{
    if (!ControlRefs.TryResolve(snapshotId, refId, out var target, out var why))
    {
        CheatState.Error("debug.click stale ref (" + why + "): take a fresh inspect snapshot");
        return false;
    }
    switch (target)
    {
        case CardUI card: Mouse.Instance.ClickOnCard(card); break;
        // uGUI Button arm is PROVISIONAL (verify-or-drop in implementation): menu buttons
        // may be custom types, in which case the UIMgr-method arm below covers menus and
        // this arm is deleted, not fixed. Do not invent a generic fallback.
        case Button button: button.onClick.Invoke(); break;
        // ... one arm per allowlisted callee, never a default-invoke
        default:
            CheatState.Error("debug.click: no invoke rule for " + target.GetType().Name);
            return false;
    }
    DebugRuntime.Emit("debug.click.done", new Dictionary<string, object>
    {
        ["ref"] = refId,
        ["type"] = target.GetType().Name,
        ["snapshot"] = ControlRefs.CurrentSnapshot()
    });
    return true;
}
```

## Testing Strategy

- **Guard (CI):** `guard-debug-scope.ps1` green.
- **Unit (no game):** ref-table lifecycle (stale detection, snapshot scoping) on synthetic
  entries only.
- **Live (real game+server):** menu click navigates (read back via `lawn/state` or the
  new screen's inspect, not the response alone); card click selects (read back via
  board-stats/selection); stale ref (restart the snapshot) fails with the recovery error.
- **Perf:** resolve is one bounded scan; invoke is one call; both on drain, never in hooks.

## Boundaries

- Always: re-resolve + type-confirm before every invoke; closed callee set (a type without
  an invoke rule is refused, never reflect-invoked — arbitrary method dispatch is out);
  scope banners; guard green.
- Ask first: adding a callee to the dispatch table; any click with side effects outside
  selection/navigation/placement (those belong in `control-act` with read-back).
- Never: OS cursor movement (that's `control-cursor`); open reflection invoke
  (`MethodInfo.Invoke` on arbitrary methods — the evaluate hole, deliberately absent);
  new Unity write paths (all callees already exist); combat/derived magnitudes (ActorHub
  gate N/A — stated).

## Success Criteria

- [ ] Click-by-ref works for one menu control, one lawn card, one cell placement —
  each read back through the normal path, not the response alone.
- [ ] Stale ref fails loudly with the fresh-snapshot instruction.
- [ ] Responses bundle the acted-upon snapshot (richer-default contract).
- [ ] Guard + scope tests green.

## Open Questions

1. uGUI `Button.onClick.Invoke()` vs engine event path (`ExecuteEvents`)? Recommendation:
   direct `Invoke()` first (simplest, proven pattern); escalate only if a live button
   demonstrably needs the full event path.
