# Spec: `control-inspect`

**Program:** `game-control` · **Map:** [../game-control-map.md](../game-control-map.md)
**Ideal:** [../game-control-ideal.md](../game-control-ideal.md) · **Standard:**
[../../contributing/live-probe-standard.md](../../contributing/live-probe-standard.md)

---

## Objective

Give an agent a budgeted, structured view of what it *could* touch on the current game
screen — Playwright's `browser_snapshot` for the lawn and menus. A single command returns
a control tree: menus as named controls, the lawn as cells/cards/living entities. Only
interactables get refs (`cN`); refs live exactly one snapshot and die on the next screen
change (native `ptr` handles are reused after death — `match-runtime.md` — so a ref is
never trusted across snapshots).

Who uses this: every other module in this program (`click`, `act`, `cursor` all consume
refs or positions from here), plus any live-probe operator doing board archaeology.
The emitted `debug.inspect` event's id IS the snapshot id downstream refs bind to.

Success: one command on a live menu returns the named controls with refs; one command on
a live lawn returns cells/cards/entities with refs; over-budget output truncates with a
`[TRUNCATED]` marker and a `next_cursor`; a second page continues where the first stopped.

**ASSUMPTIONS I'M MAKING:**

1. Menu controls enumerate from the existing `UIMgr` method table + live `CardUI`/
   `AlmanacCardUI` objects — no uGUI `GraphicRaycaster` probe in v1 (its scene presence
   is unverified; enumerations-first per the ideal).
2. Lawn geometry reuses `LawnCoords`/`TryWorldToGui` (both shipped) — no new projection.
3. Response budget defaults: page 50 controls, truncation marker mandatory (structural,
   see Boundaries).

→ Correct me now or implementation proceeds with these.

## Tech stack

- Injector C# net6 (`FindObjectsOfType<T>` over a closed type allowlist — same pattern as
  `TypeIconCapture.cs` and `DebugRuntime.BoardEntityStats()`).
- Server net8 relay + read (`DebugEndpoints.cs`, scope banners per `guard-debug-scope`).
- No new packages, no new Unity assembly references expected.

## Commands

```powershell
.\scripts\guard-debug-scope.ps1
dotnet test tests/FusionRpg.Guard.Tests --filter "FullyQualifiedName~DebugScope"
curl -X POST http://127.0.0.1:5088/api/debug/inspect -H "Content-Type: application/json" -d '{"scope":"menu","limit":50}'
```

Injector build (needs legal game dir + interop, not in CI):

```powershell
$env:FUSIONRPG_GAME_DIR = "<game folder>"
dotnet build src/FusionRpg.Injector.MelonLoader.39
```

## Project structure

| Path | Duty |
|---|---|
| `src/FusionRpg.Injector/ControlInspect.cs` (new) | Closed type allowlist; one on-demand scan per command; emits `debug.inspect` / `debug.census` (counts+samples per roster type) / `debug.scan` (full dump of one roster type: ptr, name, active, parent path, capped 200) |
| `src/FusionRpg.Injector/CheatCommandRunner.cs` | Three cases: `debug.inspect` (`{scope,limit,cursor,tag}`), `debug.census` (no args), `debug.scan` (`{type,limit,cursor}`) |
| `src/FusionRpg.Server/DebugEndpoints.cs` | `POST /api/debug/inspect` + `POST /api/debug/census` + `POST /api/debug/scan` (Game Injector Debug banners + relays); read path is `GET /api/debug/events?kinds=` (same as `board-stats` — tree payloads ride the event log, no side store) |
| `tools/debug-mcp/tools/debug_inspect.py` and sibling adapters | Eight distinguished adapters (`debug_inspect`, `debug_click`, `debug_act`, `debug_cursor`, and four `debug_evaluate_*` tools) route to the existing endpoints. They are adapter-only and use the `debug_screenshot.py` envelope convention; no `debug_control` dispatcher exists. |

## Code Style

Same as `DebugRuntime.BoardEntityStats()`: per-type try/catch so one unreadable object
never fails the scan; every temp enumerated then released; failures are
`CheatState.Error(...)` + partial result, never a throw out of the drain:

```csharp
// Structural (not tunable): bounds main-thread scan + payload. Not the balance surface.
const int InspectDefaultLimit = 50;

public static Dictionary<string, object> Inspect(string scope, int limit, string? cursor)
{
    var controls = new List<Dictionary<string, object>>();
    var n = 0;
    foreach (var card in UObject.FindObjectsOfType<CardUI>())
    {
        if (n++ >= limit) break;
        try { controls.Add(DescribeCard(card)); } catch { /* one bad card never fails the scan */ }
    }
    // ... one block per allowlisted type, same shape
    return new Dictionary<string, object>
    {
        ["controls"] = controls,
        ["truncated"] = n >= limit,
        ["next_cursor"] = n >= limit ? controls[^1]["ref"] : null
    };
}
```

## Testing Strategy

- **Guard (CI):** `guard-debug-scope.ps1` green — relay route carries `// Game Injector
  Debug`; read route carries its accurate banner.
- **Unit (no game):** ref-allocator + truncation/paging logic on synthetic lists only —
  no Unity, no temp store.
- **Live (real game+server, owner terminal):** menu snapshot shows named controls with
  refs; lawn snapshot shows cells/cards/entities; `limit=1` truncates with marker and
  `next_cursor` continues. Response-only is not proof: the check resolves one ref from
  each snapshot through the `click` module.
- **Perf:** one scan measured via `PerfProbe` (baseline expectation ~10ms/scan from
  `InjectorEntityRegistry.cs:10`); acceptance is "on-demand only, zero idle cost".

## Boundaries

- Always: on-demand scans only (never per-frame, never in a hook); per-type try/catch;
  scope banners; guard green before done.
- Ask first: widening the closed type allowlist; any read that joins across types into a
  new derived number.
- Never: open type-name-to-scan resolver (closed allowlist only); pixel-coordinate
  primary model; images in the events table; second debug surface; derived/combat
  magnitudes (ActorHub gate N/A — stated: this module produces none).

## Success Criteria

- [ ] Menu + lawn snapshots return refs for every interactable in the allowlist.
- [ ] Over-budget output carries `[TRUNCATED]` + `next_cursor`; paging continues exactly.
- [ ] `debug.census` returns counts+samples for all 24 roster types; `debug.scan Image`
  returns ptr/name/active/parent-path with truncation (both proven live 2026-09-14).
- [ ] Refs from a stale snapshot are refused by `click` (tested there, contracted here).
- [ ] Guard + scope tests green; no per-frame cost.

## Open Questions

1. Exact control-label vocabulary (who names a menu control — closed registry vs derived
   paths)? Recommendation: closed registry beside the `UiNav` action list; locked in
   implementation, not here.
