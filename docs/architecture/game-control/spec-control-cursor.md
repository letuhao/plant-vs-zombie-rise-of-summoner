# Spec: `control-cursor`

**Program:** `game-control` · **Map:** [../game-control-map.md](../game-control-map.md)
**Ideal:** [../game-control-ideal.md](../game-control-ideal.md) · **Standard:**
[../../contributing/live-probe-standard.md](../../contributing/live-probe-standard.md)
**Depends on:** `control-inspect` (positions)

---

## Objective

Move the real OS cursor and synthesize clicks — the only tier that can ever prove true
UX input path (white-box verbs prove wiring). Win32 `SendInput` move + left down/up at
screen coordinates, behind an explicit per-call opt-in, with a foreground-window check
and a rate limit. This moves the operator's actual mouse: disruptive by nature, which is
why it is a separate module with its own guards instead of a flag on `control-click`.

Who uses this: UX-path proofs only (e.g. "does the real seed-packet click path work"),
run deliberately, never as the default click.

Success: on explicit opt-in with the game foreground, the cursor moves to the requested
screen point and a click lands (read back via the game's own hook telemetry, e.g. the
`card.pick` event — not the response alone); without opt-in the command is refused;
without foreground focus it refuses rather than clicking into another window.

**ASSUMPTIONS I'M MAKING:**

1. Windows-only is acceptable (the game, the injector hosts, and the playbook are
   win-x64; P/Invoke lives beside `Hud/Win32.cs` per its own rule).
2. Coordinates are absolute screen pixels; DPI scaling is the caller's problem to state
   (the response echoes what was acted on).
3. Foreground check = game window handle is foreground (via the existing window-interop
   knowledge in `Hud/Win32.cs` + Launcher `GameWindowInterop` rule).

→ Correct me now or implementation proceeds with these.

## Tech stack

- Injector C# net6, `System.Runtime.InteropServices` P/Invoke (`SendInput`,
  `SetCursorPos` fallback documented but not primary), isolated in a `Win32Input`
  home next to `Hud/Win32.cs`. Coordinates are clamped to the game window client rect
  (via the window-handle knowledge behind `Hud/Win32.cs` + Launcher `GameWindowInterop`)
  before any move — a point outside the rect is refused, never clamped silently into a
  corner click.
- Server net8 relay with an explicit `confirmedLiveCursor: true` payload field (the
  opt-in is in the command, not a server toggle — no persistent "cursor armed" state).
- No new packages.

## Commands

```powershell
.\scripts\guard-debug-scope.ps1
dotnet test tests/FusionRpg.Guard.Tests --filter "FullyQualifiedName~DebugScope"
```

Live only (never in CI — needs a real foreground game window):

```powershell
curl -X POST http://127.0.0.1:5088/api/debug/cursor -H "Content-Type: application/json" -d '{"x":960,"y":540,"click":true,"confirmedLiveCursor":true}'
```

## Project structure

| Path | Duty |
|---|---|
| `src/FusionRpg.Injector/Hud/Win32Input.cs` (new) | `SendInput` P/Invoke (move + down/up), foreground-handle check, structural rate-limit const; every failure is a refusal with reason, never a partial click |
| `src/FusionRpg.Injector/CheatCommandRunner.cs` | One `debug.cursor` case: requires `confirmedLiveCursor:true`, requires foreground, requires rate-limit; emits `debug.cursor.done` with `{x,y,clicked,foreground}` |
| `src/FusionRpg.Server/DebugEndpoints.cs` | `POST /api/debug/cursor` (Game Injector Debug banner + relay; banner names the disruptiveness) |

## Code Style

Refuse before acting — three gates in order, each with its own error:

```csharp
// Structural (not tunable): bounds OS-cursor hijack. Not the balance surface.
const int CursorMinGapMs = 2000;

public static bool MoveClick(int x, int y, bool click, bool confirmed)
{
    if (!confirmed) { CheatState.Error("debug.cursor refused: pass confirmedLiveCursor:true"); return false; }
    if (!Win32Input.IsGameForeground()) { CheatState.Error("debug.cursor refused: game not foreground"); return false; }
    if (!RateLimiting.TryEnter("cursor", CursorMinGapMs)) { CheatState.Note("debug.cursor throttled"); return false; }
    Win32Input.Move(x, y);
    if (click) Win32Input.Click();
    DebugRuntime.Emit("debug.cursor.done", new Dictionary<string, object>
    {
        ["x"] = x, ["y"] = y, ["clicked"] = click, ["foreground"] = true
    });
    return true;
}
```

## Testing Strategy

- **Guard (CI):** `guard-debug-scope.ps1` green. No automated cursor tests in CI —
  moving a CI machine's cursor is out of order; this module's tests are live-only plus
  unit tests for the refusal logic (opt-in parsing, rate-limit) on synthetic state.
- **Live (real game+server, owner terminal, owner watching):** cursor visibly moves;
  click on a seed packet selects it (read back via `card.pick` event); background-window
  case refuses with the foreground error; second rapid call throttles.
- **Perf:** N/A beyond the rate limit (OS calls, no scan).

## Boundaries

- Always: explicit per-call opt-in; foreground check; rate limit; refusal reasons (never
  silent); scope banner naming disruptiveness; guard green.
- Ask first: any new P/Invoke beyond move/click/foreground-check (drag/wheel are named
  follow-ups, not v1); changing the rate limit.
- Never: default-on cursor movement; clicking without foreground focus; moving the cursor
  from any ambient/per-frame path (command-only, rate-limited); combat/derived magnitudes
  (ActorHub gate N/A — stated).

## Success Criteria

- [ ] Opt-in foreground click lands and is read back via game telemetry.
- [ ] Missing opt-in, background window, and rapid repeat all refuse with distinct reasons.
- [ ] No CI test moves any cursor; refusal logic unit-tested.
- [ ] Guard + scope tests green.

## Open Questions

None — owner placed this tier in v1; the only remaining choice (drag/wheel follow-ups)
is explicitly deferred above.
