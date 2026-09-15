# Spec: `tombstone`

**Program:** `rift-gate` · **Map:** [../rift-gate-map.md](../rift-gate-map.md)
**Ideal:** [../rift-gate-ideal.md](../rift-gate-ideal.md) — decisions 11, 15
**Touches:** `src/FusionRpg.Injector/Hud/RiftMenuOverlay.cs`, a new interactive sibling,
`src/FusionRpg.Injector/Hud/OverlaySwitchGui.cs`, its two host `OnGUI` call sites
**Depends on:** `menu-anchor` (the reviewed signal + hit-box contract)

---

## Objective

Make the **cracked tombstone clickable** on the reviewed main menu, route that click to the **existing**
toggle convergence point, and correct the shipped art's over-broad draw gate — plus, per decision 15,
restyle the **existing guard-pinned in-match "RPG" button** as the one live-lawn affordance.

**The transport and the toggle already exist and are not touched.** The tombstone is a
**discoverability** affordance, not a capability fix: F10 already opens the overlay from the main menu
in both host modes (`MainWindow.xaml.cs:270` global `RegisterHotKey`, handler `:301-308`; injector
`OverlaySwitch.cs:113-117` + `OverlayViewHost.cs:186`). A click must reach the **same** toggle the
hotkey and the pipe reach — `MainWindow.xaml.cs:277` *"Same toggle path as the hotkey — no second
behavior"* — so the tombstone emits the **existing** `toggle` verb over the **existing** pipe
(`OverlayPipeServer.ParseCommand`, `:81-86`) or, injector-hosted, calls the in-process
`OverlayViewHost.Toggle()` (`:65`) exactly as `OverlaySwitch` already does (`OverlaySwitch.cs:130`).

**Two jobs, one file each:**

1. **Paint vs click are separate layers** (ideal §"The shape being proposed" 1). `RiftMenuOverlay`
   keeps painting the art under its current broad gate (`RiftMenuOverlay.cs:25`,
   `GameHooks.Board != null` → return); the new interactive sibling draws the hit target **only** on
   the reviewed signal from `menu-anchor`, never on `Board == null`. This is map gap 4: the shipped art
   already paints on the seed picker, the Almanac, every submenu, and defeated-board states, because
   `Board == null` is not "the main menu" (`DebugActions.cs:1470-1471`). Correcting the **click** gate
   is this module's job; the paint gate is narrowed only to the extent the reviewed signal allows.
2. **The in-match "RPG" button is restyled, not replaced** (decision 15). There is exactly **one** lawn
   affordance. `OverlaySwitchGui` (`:40`, `GUI.Button(_rect, "RPG")`) keeps its single action
   (show/hide) and its visibility gate; only its icon/label presentation changes to the tombstone
   treatment. The guard/test that pins its single-action identity is **updated, not bypassed**.

**Success:** on the reviewed main menu a click on the tombstone opens the web FE (same result as F10);
on every other surface there is no interactive hit target; the art no longer paints where nobody
reviewed it; the in-match button still carries exactly one action.

**ASSUMPTIONS I'M MAKING:**

1. The interactive sibling uses the `OverlaySwitchGui` event filter verbatim — `Repaint`, `Layout`,
   `MouseDown`, `MouseUp` (`OverlaySwitchGui.cs:26-30`) — because an interactive IMGUI control cannot
   be Repaint-gated (its own doc `:10-12` states this). A Repaint-only gate is the paint layer's.
2. **Click routes through `OverlaySwitch.RequestToggle()`** (`:44`), not a second toggle path. In
   launcher mode that is the pipe `toggle` verb; in injector mode `TickInjectorHosted` calls
   `OverlayViewHost.Toggle()` (`:130`). One request path, two sinks — matching `overlay-hide`'s own
   dual-host shape.
3. The host `OnGUI` chains each gain **one** call line beside the existing four
   (`Plugin.cs:39-42`; `MelonFusionRpgMod.cs:76-79`), keeping the single-IMGUI-entry rule both hosts
   document (`MelonFusionRpgMod.cs:68-70`).
4. **Gap 6 decision: yes, the preference still suppresses the in-match button while the menu entry
   stays live — they are different affordances.** `OverlaySwitchState.ButtonVisible` (`:51`) keeps
   `SettingsEnabled`; the menu tombstone is gated on the `menu-anchor` signal, not on `SettingsEnabled`.
   But `OverlaySwitch.cs:111-117` starts the injector host **only** when `State.SettingsEnabled` is
   true, with the comment *"with the button off there is no other way to open it in this mode"* — once
   the tombstone is an independent entry that comment is false and the view-start condition must stop
   keying off the button preference. This is a **wiring-gap fix**, owned here.

→ Correct me now or implementation proceeds with these.

## Tech stack

- Injector C# net6 + IMGUI (the existing `OnGUI` chain — no new draw surface, no new host hook).
- New Core pure geometry reuse: the hit box comes from `menu-anchor`'s `RiftMenuOverlayLayout.HitBox`,
  tested from nowhere (the `OverlaySwitchLayout` precedent, `OverlaySwitchLayout.cs:39-62`).
- No new packages, no new Unity references, no new tuning file (the `riftMenu` group is `menu-anchor`'s).

## Commands

```powershell
# Pure geometry (Core; no game, no unity)
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Overlay"

# Boundary selected for these paths (never the whole suite — AGENTS.md verification boundary)
.\scripts\verify-change.ps1 -Paths src/FusionRpg.Injector/Hud/RiftMenuOverlay.cs,src/FusionRpg.Injector/Hud/OverlaySwitchGui.cs,src/FusionRpg.Injector/Hud/OverlaySwitch.cs -Session rift-gate-spec-20260915-c41a

# The pipe contract guard must stay green (no new verb is added by this module)
dotnet test tests/FusionRpg.Guard.Tests --filter "FullyQualifiedName~OverlayPipeContract"

# Injector compiles with real interop (the part CI cannot build)
dotnet build src/FusionRpg.Injector.MelonLoader.39 -c Release -p:MlGameDir=$env:FUSIONRPG_ML_GAMEDIR -p:GameProfile=pvzrh-3.9
```

## Project structure

| Path | Duty |
|---|---|
| `src/FusionRpg.Injector/Hud/RiftMenuOverlay.cs` | Keeps the paint layer. Its gate is **narrowed** to the reviewed signal where the art is interactive-adjacent; the broad `Board == null` paint may remain for the non-interactive art (Boundaries) |
| `src/FusionRpg.Injector/Hud/RiftMenuTombstoneGui.cs` (new) | The **interactive** sibling: `OverlaySwitchGui`'s event filter (`:26-30`) + `menu-anchor`'s `AllowsInteractiveAffordance(boardIsLive)` gate + `menu-anchor`'s `HitBox` + `OverlaySwitch.RequestToggle()` on click. One cached rect, one `GUI.Button`, no allocation |
| `src/FusionRpg.Injector/Hud/OverlaySwitchGui.cs` | Restyle only: label/icon becomes the tombstone treatment. `GUI.Button(_rect, "RPG")` (`:40`) → the restyled control. Its single action (`OverlaySwitch.RequestToggle()`, `:41`) and gate (`OverlaySwitch.ButtonVisible`, `:23`) are unchanged |
| `src/FusionRpg.Injector/Hud/OverlaySwitch.cs` | Gap 6: the injector-host view-start condition (`:111-117`) stops keying off the **button** preference once the menu tombstone is an independent entry |
| `src/FusionRpg.Injector.BepInEx/Plugin.cs` · `src/FusionRpg.Injector.MelonLoader/MelonFusionRpgMod.cs` | One added draw line each, beside the existing four (`Plugin.cs:39-42`; `MelonFusionRpgMod.cs:76-79`) |

## Code Style

Match `OverlaySwitchGui`: a static class, a cached `Rect` recomputed only on resize, the exact
interactive event filter, and no throw escaping into `OnGUI`. The doc comment states what the control
does **not** do (one action; never a cheats surface).

```csharp
/// <summary>
/// The clickable tombstone on the reviewed PVZ main menu. One action: open/close the web FE
/// through the same request path the in-game button and the hotkey use. It is not a cheats
/// surface and it is never drawn on a reviewed-excluded surface (a live board, the pause menu).
/// </summary>
public static class RiftMenuTombstoneGui
{
    public static void Draw()
    {
        try
        {
            if (!RiftMenuAnchor.AllowsInteractiveAffordance(GameHooks.Board != null)) return;
            var e = Event.current;
            if (e == null) return;
            if (e.type != EventType.Repaint && e.type != EventType.Layout
                && e.type != EventType.MouseDown && e.type != EventType.MouseUp) return;

            var hit = RiftMenuOverlayLayout.HitBox(Screen.width, Screen.height);
            var rect = new Rect(hit.X, hit.Y, hit.Width, hit.Height);
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none)) OverlaySwitch.RequestToggle();
        }
        catch { }
    }
}
```

## Testing Strategy

- **Geometry, pinned in Core (not Unity).** `HitBox` is tested in `FusionRpg.Core.Tests` exactly as
  `OverlaySwitchLayout` is (`OverlaySwitchLayoutTests.cs`): a stable axis-aligned rect across the
  wobble's angle range, positive and on-screen at `(0,0)`, 720p, 1080p, 4K, and a minimum device-pixel
  size read from `riftMenu`. **No test asserts a population count** — these are structural bounds
  (a rect is on-screen, a size is at least N), never a derived roster/species number.
- **The gate is the reviewed signal, not `Board == null`.** A test asserts the interactive path is
  unreachable when `menu-anchor` reports a non-reviewed surface **even with `Board == null`** — the
  exact regression the seed-picker/Almanac paint defect represents. This is a contract assertion, not a
  count.
- **The single-action identity is preserved, and the guard is updated with it.** There is no automated
  literal pin on the `"RPG"` string today (grep confirms; the pin is the doc at `OverlaySwitchGui.cs:7`
  plus its behaviour), so the "guard/test updated, not bypassed" work is: keep
  `OverlaySwitch.RequestToggle()` as the button's only call and add a test asserting the restyled
  control still has exactly one action (one `GUI.Button`, one request call). If a literal pin is added
  during implementation, it moves with the label in the same commit.
- **Pipe contract unchanged.** `OverlayPipeContractGuardTests` (`:39-57`) asserts every verb the client
  sends is one the server accepts; this module adds **no** verb, so that guard must stay green
  untouched — a red there means the click grew its own path, which is the defect.
- **Live (owner terminal):** main-menu click opens the overlay (same as F10); on the seed picker and
  the Almanac the art may paint but there is **no** hit target; in-match the restyled button still
  toggles once and only once (debounce). Read the change back through the normal path
  (`live-probe-standard.md` §3) — a screenshot of a click isn't the proof; the overlay actually
  covering the game is.
- **Unverified by CI, stated honestly:** Win32, WebView2, IMGUI, and the real host `OnGUI` chains stay
  untested in CI (`overlay-spec.md:154`), matching the shipped overlay's own testing strategy.

## Boundaries

- **Always:** route the click through `OverlaySwitch.RequestToggle()`; keep the paint layer and the
  interactive layer separate; keep the in-match button's single action; keep every failure swallowed
  and logged (no silent no-op); recompute the hit rect only on resize.
- **Ask first:** adding a **second** action to the in-match button; making the tombstone interactive on
  any surface beyond the reviewed allow-list; moving the art's broad paint gate.
- **Never:** a second toggle path or a second pipe verb; a cheats surface on the tombstone; drawing the
  interactive hit target on a live board, the pause menu, or any non-reviewed surface; a new draw
  surface or host hook; binding F10 in the FE (`keymap.ts:18` forbids it,
  `keymapGuard.test.ts:10-11` asserts it).
- **ActorHub gate: N/A.** This module produces and consumes no actor combat/derived/AppliedCombat
  magnitude. It folds no actor number and reads no Hub output.
- **No runtime code in this phase** (spec phase); this section governs the build that follows.

## Tunables

| Number | Home | Why |
|---|---|---|
| Tombstone placement/size percentages | `data/tuning/overlay.v1.json` → `riftMenu` (**owned by `menu-anchor`**, read here) | A balance-feel pass would change these; extracted from `RiftMenuOverlayLayout.cs:30-42` exactly as `switchLayout` was (`overlay.v1.json:4-7,13-20`) |
| Minimum hit-target device px | same group | Same class |
| In-match button geometry | `overlay.v1.json` → `switchLayout` (already exists, `:13-20`) | Unchanged by the restyle — the restyle is presentation, not geometry |

A missing `riftMenu` key is a **load rejection naming it** (`OverlayTuningLoader.Parse` reads
`switchLayout`/`switchState`/`settingsGui` by name and throws on absence, `OverlayTuning.cs:42-65`),
never a default. `publish.py` refuses to invent a key (`publish.py:130-131`), so the group is authored
once by hand in the `menu-anchor` commit.

## Success Criteria

- [ ] A click on the reviewed main-menu tombstone opens the web FE, identical to F10 in both host modes.
- [ ] The click routes through `OverlaySwitch.RequestToggle()`; no second toggle path, no new pipe verb
      (`OverlayPipeContractGuardTests` untouched and green).
- [ ] The interactive hit target exists **only** on the reviewed signal; absent on the seed picker,
      Almanac, submenus, live boards, and the pause menu.
- [ ] `HitBox` is axis-aligned, wobble-independent, positive/on-screen at `(0,0)`/720p/1080p/4K, with a
      minimum device-pixel target from `riftMenu`.
- [ ] The in-match "RPG" button keeps exactly one action (show/hide); the restyle is presentation only.
- [ ] Gap 6 fixed: with the button preference off in injector mode, the menu tombstone still opens the
      view; the preference suppresses only the in-match button.
- [ ] Each host `OnGUI` chain gains exactly one line; the single-IMGUI-entry rule holds.
- [ ] ActorHub gate N/A stated; no server round trip; no `data/tuning` number introduced beyond the
      `riftMenu` group read.

## Open Questions

1. **Does the broad paint gate get narrowed now or deferred?** The ideal keeps paint broad
   (`Board == null`) deliberately — the art is atmospheric and harmless where it paints. This spec
   corrects the **click** gate (the hazard) and leaves paint as-is except for a note. If the owner
   wants the paint narrowed too, that is a reviewed change to `RiftMenuOverlay.cs:25` and its own
   acceptance, not implied here.
2. **The second reviewed surface** (map decision 11) is `menu-anchor`'s open question; the tombstone
   inherits whatever the enum grows to, with no code change beyond the shared gate.
