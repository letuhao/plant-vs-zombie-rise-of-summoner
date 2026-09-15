# Spec: `menu-anchor`

**Program:** `rift-gate` · **Map:** [../rift-gate-map.md](../rift-gate-map.md)
**Ideal:** [../rift-gate-ideal.md](../rift-gate-ideal.md)
**Depends on:** — (this module is a dependency of `tombstone` only)

---

## Objective

Emit a **narrow, reviewed menu-state signal** — "a named, safe PVZ menu is open right now" — and own
the **hit-target geometry contract** the tombstone consumes. This is the one genuinely new mechanism
the doorway needs; everything else in the program is a patch extension, a restyle, or a contract.

Today the only menu knowledge the injector has is `GameHooks.Board == null`
(`src/FusionRpg.Injector/GameHooks.cs:19`), which is also the seed picker, the almanac, every
submenu, and the defeated-board states — `DebugActions.cs:1470-1471` names exactly those ambiguous
cases and refuses to guess. That inference is why the shipped art already paints on
surfaces nobody reviewed (map gap 4). **Painting may stay broad; clicking may not.**

**State signal, not an event.** The map (gap 10) records the trap: this is *not* "a menu was
entered" (the one-shot `menu.enter` emit at `GameCaptureHooks.cs:812-818`). It is "surface X is open
right now", so **every way out of X must clear it**. DESIGN-GATE §2.16's key-set edge applies by
shape: the edge that gets forgotten is the transition into *any other* surface, so the exit-edge
list below is the deliverable, and each exit edge carries its own test.

**Who uses this:** `tombstone` consumes the signal and the hit-box contract; it never inspects host
UI itself. `menu-anchor` is never sent to the server and is never on the hit path (map: "Two state
machines, no shared state — only messages").

**Success:** on a reviewed surface the signal reads surface-`X`-open; on every listed exit edge it
reads `None` before the next frame is drawn; the hit box is a stable axis-aligned rect derived from
layout, with a device-pixel minimum, that never overlaps a host primary action.

**ASSUMPTIONS I'M MAKING:**

1. `UIMgr.EnterMainMenu` is a real callable static — used by the debug nav switch
   (`DebugActions.cs:1529`) and proven safe to call unconditionally from any menu depth
   (`DebugActions.cs:1523-1527`) — but it is **not yet patched**. Adding that patch is the same
   shape as the three beside it (`GameCaptureHooks.cs:780,790,812`).
2. The reviewed allow-list is the **main menu only** in v1. The map's decision 11 asks for "one
   reviewed non-gameplay menu surface"; nothing in the code today proves a second safe surface, so
   v1 ships the closed enum with one member and widening it is an ask-first review.
3. **The pause menu is excluded by name** (map gap 7). It sits over a live board — the exact hazard
   the board gate at `OverlaySwitchState.cs:44-48` avoids.
4. The hit box is derived from the **layout** rect, never the painted rect: the art is drawn rotated
   and wobbling (`RiftMenuOverlay.cs:78`, `:53-55`) while IMGUI hit testing is axis-aligned.

→ Correct me now or implementation proceeds with these.

## Tech stack

- Injector C# net6 + Harmony. One new patch beside `EnterPauseMenu` / `BackToGame` / `BackToMenu`
  (`GameCaptureHooks.cs:780,790,812`) plus a new **pure Core** state carrier, matching the
  `OverlaySwitchState` precedent (Unity-free, clock-injected, testable from nowhere —
  `OverlaySwitchState.cs:1-8`).
- No new packages, no new Unity assembly references, no new draw surface.
- One new `data/tuning/overlay.v1.json` group (`riftMenu`) for the placement/size/minimum-hit numbers
  — see Tunables.

## Commands

```powershell
# Pure state + geometry (Core; no game, no unity)
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Overlay"

# Boundary selected for these paths (never the whole suite — AGENTS.md verification boundary)
.\scripts\verify-change.ps1 -Paths src/FusionRpg.Core/Overlay/RiftMenuAnchor.cs,src/FusionRpg.Core/Overlay/RiftMenuOverlayLayout.cs,src/FusionRpg.Injector/GameCaptureHooks.cs -Session rift-gate-spec-20260915-c41a

# Injector compiles with real interop (the patch is the part CI cannot build)
dotnet build src/FusionRpg.Injector.MelonLoader.39 -c Release -p:MlGameDir=$env:FUSIONRPG_ML_GAMEDIR -p:GameProfile=pvzrh-3.9
```

## Project structure

| Path | Duty |
|---|---|
| `src/FusionRpg.Core/Overlay/RiftMenuAnchor.cs` (new) | **Closed surface enum** + pure state carrier: `RiftMenuSurface { None, MainMenu }`, `RiftMenuAnchorState` with `Set/RawRead` semantics, and `IsReviewed(surface)`. Unity-free, clock-injected. The enum is a **closed vocabulary** the code owns (see Testing Strategy for the literal-pin rule) |
| `src/FusionRpg.Core/Overlay/RiftMenuOverlayLayout.cs` | Gains the **hit-box** derivation: `HitBox(int w, int h)` returning a stable axis-aligned `RiftOverlayRect` from the same percentages as `TopMiddleLeft` (`:44-53`), plus `MinimumHitDevicePx` and `MeetsMinimumHitTarget(...)` — the two new rules the ideal's real-gap table asks for at `rift-gate-ideal.md:129` |
| `src/FusionRpg.Injector/GameCaptureHooks.cs` | One new patch: `[HarmonyPatch(typeof(UIMgr), nameof(UIMgr.EnterMainMenu))]` → `RiftMenuAnchor.Set(RiftMenuSurface.MainMenu)`. Plus the **exit edges** on the already-patched methods (`EnterPauseMenu` `:780`, `BackToGame` `:790`, `BackToMenu` `:812`) → `Set(None)`; `BackToMenu` already emits `menu.enter` (`:818`) and this patch sits beside that emit, not instead of it |
| `src/FusionRpg.Injector/Hud/RiftMenuOverlay.cs` | Reads the anchor for its **draw gate** where it paints an interactive affordance (the art's own broad gate stays for the non-interactive paint) — see Boundaries |
| `data/tuning/overlay.v1.json` | New `riftMenu` group (placement/size/minimum hit target) |
| `tests/FusionRpg.Core.Tests/Overlay/RiftMenuAnchorTests.cs` (new) | The exit-edge test matrix (one test per edge) |

## Code Style

Mirror `OverlaySwitchState`: a sealed, pure class with an injected clock, no static mutable state
read from Unity, and a doc comment that says what the class does *not* do.

```csharp
/// <summary>
/// Which reviewed PVZ menu surface is open right now. Read by the tombstone's draw gate only —
/// never sent to the server, never on the hit path, never a combat/domain input.
///
/// A STATE, not an event: every way off a reviewed surface must call Set(None). Clearing is the
/// load-bearing half, so each exit edge has its own test (RiftMenuAnchorTests).
/// </summary>
public sealed class RiftMenuAnchor
{
    public RiftMenuSurface Current { get; private set; } = RiftMenuSurface.None;

    public void Set(RiftMenuSurface surface) => Current = surface;

    /// <summary>True only for a surface a human reviewed as non-gameplay and non-pause.</summary>
    public bool IsReviewed => RiftMenuAnchorPolicy.IsReviewed(Current);

    /// <summary>A board appearing is always an exit — the art's own broad gate already does this.</summary>
    public bool AllowsInteractiveAffordance(bool boardIsLive) => IsReviewed && !boardIsLive;
}
```

## Testing Strategy

- **Closed-enum pin (correct here, with its reason).** `RiftMenuSurface` is a **closed vocabulary the
  code owns and a human reviews** — not a derived population — so pinning its literal membership in
  a test is correct per DESIGN-GATE §3.7 and the map's own interface rule
  (`rift-gate-map.md:74-77`). The test states *why* it is pinned. No test asserts any count that
  grows with content or generated data.
- **Exit edges get one test each.** This is the module's real work; a criterion that names only
  "entering another menu clears it" would leave five edges untested. The matrix:
  `enter-match / open-submenu / open-pause-menu / open-modal / lose-board / game-quit` → all `None`.
  `BackToMenu` landing one layer *short* of the main menu (`DebugActions.cs:1517-1527` documents the
  live proof: `BackToMenu()` alone reaches the Challenge-Mode select, so the same action chains
  `EnterMainMenu()`) is itself an exit edge for the previous surface and an entry for `MainMenu` only
  once `EnterMainMenu` is reached — both directions tested.
- **Order-independence.** `Set(MainMenu)` immediately followed by `Set(None)` and the reverse both
  clear; no ordering is assumed (DESIGN-GATE §2.16 corollary).
- **Geometry.** `HitBox` is axis-aligned and **independent of the wobble** — the test drives
  `HitBox` at a fixed size across the pulse's angle range and asserts the rect is byte-identical
  (the art's `RotateAroundPivot` at `RiftMenuOverlay.cs:78` cannot move it). Minimum-hit rule is
  tested at degenerate sizes (`(0,0)`, 720p, 4K) and always returns a positive, on-screen rect.
- **Live (owner terminal):** enter the main menu → signal reads `MainMenu`; open the pause menu from
  a live board → reads `None`; lose a board → `None`; the tombstone's hit box tracks layout, not the
  wobble. A response body is not proof — the changed signal is read back through the same accessor
  the tombstone uses (`live-probe-standard.md` §3).

## Boundaries

- **Always:** the signal is a read of host UI state for an affordance; every exit edge calls
  `Set(None)`; the hit box derives from layout; the enum stays closed.
- **Ask first:** widening the reviewed allow-list beyond `MainMenu`; patching any additional `UIMgr`
  static; changing the minimum device-pixel hit target.
- **Never:** send the signal to the server; put it on the hit path; use `Board == null` as the
  signal; include the pause menu; add a second menu-detection mechanism beside this one; an
  event-refreshed cache (N/A — the signal is a pure read, no hydration, no key set).
- **ActorHub gate: N/A.** This module produces and consumes no actor combat/derived/AppliedCombat
  magnitude. It folds no actor number, reads no Hub output, and must not invent one.
- **No tuning number in code** except via the `riftMenu` group; no cap on a magnitude exists here.

## Tunables

| Number | Home | Why |
|---|---|---|
| Tombstone placement/size percentages (today `const` at `RiftMenuOverlayLayout.cs:30-42`) | `data/tuning/overlay.v1.json` → `riftMenu` | A balance-feel pass would change these, so they are the balance surface (`tunables-ssot.md` T1). Precedent: `switchLayout` was extracted from `OverlaySwitchLayout.cs` into the same file (`overlay.v1.json:4-7,13-20`) |
| Hit-target minimum size (device px) + its scale-vs-screen rule | same file, beside `switchLayout` | Same class; the derive-from-height precedent is `OverlaySwitchLayout.ScaleFor` (`OverlaySwitchLayout.cs:39-45`) |
| Hit-box padding (if any) | same group | Same class |

`data/tuning/overlay.v1.json:7` says *"Never hand-edit this file"* — `tools/tuning/publish.py`
**edits existing tunables only and refuses to invent a key** (`publish.py:130-131`). So first author
the `riftMenu` group in `overlay.v1.json` by hand once (documented in the commit), exactly as the
story-scene program recorded for its own new tuning file; after that it is publishable. A missing
`riftMenu` key is a **load rejection naming it** (`OverlayTuningLoader.cs:72-91`), never a default
(`tunables-ssot.md` T5).

## Success Criteria

- [ ] `RiftMenuSurface` is a closed enum with one reviewed member (`MainMenu`) plus `None`; the
      pinned-literal test names the closed-vocabulary reason.
- [ ] `EnterMainMenu` is patched; the signal reads `MainMenu` on the true main menu.
- [ ] **Every** exit edge (match / submenu / pause / modal / lose / quit) clears to `None`, one test
      each; no edge is inferred from another.
- [ ] `HitBox` is axis-aligned, wobble-independent, and positive/on-screen at `(0,0)`, 720p, 1080p,
      and 4K.
- [ ] The minimum device-pixel hit target is enforced and read from `riftMenu`, not a literal.
- [ ] The pause menu is in the enum's excluded set **by name**, with the reason recorded.
- [ ] No per-frame allocation; the signal read is O(1).
- [ ] ActorHub gate N/A stated; no server round trip; no `Board == null` signal.

## Open Questions

1. **The second reviewed non-gameplay surface.** Decision 11 asks for "one reviewed non-gameplay
   menu surface" beyond the main menu. The code proves no second safe surface today. v1 ships the
   enum with one member; which surface becomes the second is an owner review at widen time, not a
   spec-time guess. (Non-blocking: the module is spec-able and buildable with one member.)
2. **Modal detection.** "Open a modal" is a real exit edge but no `UIMgr` static names a modal
   directly; v1 clears on the `EnterPauseMenu`/`BackToGame`/`EnterMainMenu` set it can observe and
   treats an unobserved modal as a **known blind spot** rather than inventing a probe. If live
   testing shows a modal that leaves the signal stale, that is a real defect and a second patch —
   recorded, not assumed away.
