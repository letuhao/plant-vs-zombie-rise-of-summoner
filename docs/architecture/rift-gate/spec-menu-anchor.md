# Spec: `menu-anchor` (rewritten to map Decision 17)

**Program:** `rift-gate` · **Map:** [../rift-gate-map.md](../rift-gate-map.md) — **Decision 17 (owner, 2026-09-15)**
**Ideal:** [../rift-gate-ideal.md](../rift-gate-ideal.md)
**Supersedes:** this spec's first draft (a `UIMgr.EnterMainMenu` patch + a six-edge exit matrix).
**Depends on:** — (this module is a dependency of `tombstone` only)

> **Read this first.** Map Decision 17 supersedes Gap 10 and Correction 3's mechanism. The owner's rule:
> **switch the WebView; never fight the PVZ engine.** `UIMgr.BackToMenu` / `EnterMainMenu` are
> **forbidden** — a documented live hazard ("forced entry … can destabilize the engine",
> `DebugActions.cs:1495-1496`; `DebugEndpoints.cs:759-760`), `BackToMenu` is proven to land one menu
> layer short (`live-test-ssot.md:242-251`), and calling them **mid-run breaks a live run**. The first
> draft patched a nav static; that is exactly the pattern this rewrite removes.

---

## Objective

Emit a **menu-screen presence signal** — "the PVZ main menu screen exists right now" — by patching the
**menu screen's own lifecycle**, never a navigation static. Hold a boolean. Expose a **closed surface
enum**. Own the placement contract the uGUI affordance consumes.

**Presence, not navigation.** The reference implementation
([`darkthemer/PvZF_MainMenuFlowers`](https://github.com/darkthemer/PvZF_MainMenuFlowers)) patches
`[HarmonyPatch(typeof(MainMenu), "Start")]` (`FlowerClickable.cs`) — a postfix that runs when the
main-menu object actually exists. That is a **fact about the screen**, not a hope that a navigation call
routes through a particular static. `MainMenu.Start` is a **real, private method** in our 3.9 interop
(verified below), so the patch is by **string name**, exactly as the reference does — `nameof` cannot
name a private member across the assembly boundary.

**The real work is now presence *semantics*, not six exit edges.** The first draft hand-enumerated
match/submenu/pause/modal/lose/quit as exit edges, each needing its own test. Under Decision 17 that
machinery **dissolves**, but not for the reason the map states — and the correction matters:

- The map assumed "Unity destroys it with the menu", so exit would be `OnDestroy`. **Verified: `BaseMenu`
  (the base of `MainMenu`) declares `Awake`, `OnExit`, `OnHide`, `OnBackEnter`, `PopMenu`,
  `BackToMainMenu` — and does NOT declare `OnDestroy`.** The menus are **hidden and re-shown** (they
  carry `canvasGroup`, `anim`, `collider2Ds`), not necessarily destroyed and re-`Start`ed. So the signal
  must clear on the screen's **hide/exit lifecycle callbacks** (`OnHide` / `OnExit`, both `virtual` on
  `BaseMenu`, so a patch on the base catches `MainMenu`), guarded by `__instance is MainMenu`.
- **Both are lifecycle callbacks, not navigation methods.** Patching them is *consuming presence*; it is
  not driving the engine's menu state. The forbidden thing is *calling* a nav static, and this spec
  calls none.
- The **affordance itself needs no clearing at all** (`tombstone`): it is parented into the menu's own
  hierarchy, so the menu's own show/hide takes it with it. The boolean exists for other consumers and for
  tests, and it clears on the screen's own lifecycle.

**Who uses this:** `tombstone` consumes the presence signal and the placement contract; it never patches
a nav static itself. `menu-anchor` is never sent to the server and is never on the hit path.

**Success:** the signal reads `MainMenu` after the main menu's `Start` runs; it clears when the screen
hides/exits; it never clears because some other menu hid; no rift-gate code calls a `UIMgr` navigation
method anywhere.

**ASSUMPTIONS I'M MAKING:**

1. **The reviewed allow-list is `MainMenu` only in v1.** The map's decision 11 asks for "one reviewed
   non-gameplay menu surface"; the code proves no second safe surface, so the closed enum ships one
   member and widening is an ask-first review.
2. **The pause menu is excluded by name.** It sits over a live board — the hazard the board gate at
   `OverlaySwitchState.cs:44-48` avoids. It is not a `MainMenu`, so the type-guarded patch cannot set it.
3. **The signal is a plain static boolean on a Core class**, not an event and not a cache: it is written
   by two patched callbacks and read O(1) by the affordance's draw path. No hydration, no key set,
   no invalidation — so DESIGN-GATE §2.16's event-refreshed-cache rule is **N/A** (stated, not assumed).
4. **The 3.9 verification is Task 1's one-time check, not a risk to carry** (owner: *no one redesigns a
   main-menu hierarchy between versions*) — see "Verified in this session".

→ Correct me now or implementation proceeds with these.

## Verified in this session (3.9, MelonLoader interop)

Checked by decompiling the real 3.9 interop assembly
(`H:\Games\PVZ-Fusion-3.9_MelonLoader\MelonLoader\Il2CppAssemblies\Assembly-CSharp.dll`, via `ilspycmd`):

| Claim | Result |
|---|---|
| The type `MainMenu` exists in our 3.9 interop | **Verified** — `Il2Cpp.MainMenu : BaseMenu` |
| `MainMenu` has an entry method named `Start` | **Verified** — `Start_Private_Void` (`MainMenu`'s own method, token 100674800). It is **private**, so patch by string name `"Start"` |
| `MainMenu`'s lifecycle for clearing | **Verified present on `BaseMenu`:** `OnHide`, `OnExit`, `OnBackEnter`, `BackToMainMenu`, `PopMenu` (all `virtual`). **`OnDestroy` is NOT declared on `BaseMenu`/`MainMenu`** — the map's "clears with it via OnDestroy" is corrected to the hide/exit callbacks |
| Our csproj can express the pattern | **Verified** — `UnityEngine.UI`, `Il2CppInterop.Runtime`, `Assembly-CSharp` referenced (`FusionRpg.Injector.MelonLoader.39.csproj:84-93,105-129`); `global using Il2Cpp;` present (`GlobalUsings.Il2Cpp.cs:3`) |

**Version drift is a verification step, not a risk** (owner, 2026-09-15): *no one redesigns a main-menu
hierarchy between versions.* Task 1 confirms **once**, against the real 3.9 interop, that `MainMenu`
exposes the patch target and that the hierarchy paths resolve. That is a single check at the start of
implementation, **not** a hedge this design carries. Verified here already: the type, the `Start` method,
and the lifecycle callbacks — so what remains for Task 1 is the live path resolution below.

**Remaining verification for Task 1 (settled there, not carried as a program risk):**

- **The hierarchy object names.** `Grave/LowerButtons`, `LanguagesButton`, `UpdateInfoButton`,
  `GraveBackground` are the reference's paths under **3.8.1**. A scene object's name lives in the scene,
  not in `Assembly-CSharp.dll`, so a DLL string search cannot settle it. Task 1 resolves them live **once**
  (per the owner: the architecture does not change between versions), and the code degrades to a **named
  warning** when a path misses — never a null-deref, never a silent no-op.
- **Whether `OnHide`/`OnExit` fire on every real exit path** (including the game's own animated
  transitions). Task 1 confirms live; a path that hides `MainMenu` without either firing is a real defect
  and a named follow-up, not something to paper over.
- **The BepInEx host is secondary and settled, not open.** On this machine `BepInEx/` contains only
  `plugins/` (no `interop/`) and `deploy-play.ps1:5` calls that install *"the older 3.8.1 install, kept
  for BepInEx-specific testing"*; **MelonLoader is the deployed default** (`deploy-play.ps1:39,115`).
  **MelonLoader is the first target.** Whether the BepInEx interop exposes `MainMenu` is checked when that
  host is touched; if not, the tombstone is MelonLoader-only and BepInEx keeps **F10** — a scoped
  difference stated plainly, not a design input.

## Tech stack

- Injector C# net6 + Harmony. **One patch on the menu screen's lifecycle**, in the shared injector
  sources (`src/FusionRpg.Injector/**`), host-gated because the type only resolves on the Melon host.
- A new **pure Core** state carrier (`RiftMenuAnchor`), Unity-free and directly testable, matching the
  `OverlaySwitchState` precedent (`OverlaySwitchState.cs:1-8`).
- No new packages. No new Unity reference (already present). No new draw surface (the affordance is
  `tombstone`'s).
- One `data/tuning/overlay.v1.json` group (`riftMenu`) for the affordance's placement/size — see
  Tunables.

## Commands

```powershell
# Presence state (Core; no game, no unity)
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~RiftMenuAnchor"

# Boundary selected for these paths (never the whole suite — AGENTS.md verification boundary)
.\scripts\verify-change.ps1 -Paths src/FusionRpg.Core/Overlay/RiftMenuAnchor.cs,src/FusionRpg.Injector/Hud/MenuPresenceHook.cs -Session rift-gate-spec-20260915-c41a

# Injector compiles with real interop (the patch is the part CI cannot build)
dotnet build src/FusionRpg.Injector.MelonLoader.39 -c Release -p:MlGameDir=$env:FUSIONRPG_ML_GAMEDIR -p:GameProfile=pvzrh-3.9

# 3.9 type check, when the interop is present (the verification Task 1 runs first)
ilspycmd -t Il2Cpp.MainMenu "$env:FUSIONRPG_ML_GAMEDIR\MelonLoader\Il2CppAssemblies\Assembly-CSharp.dll"
```

## Project structure

| Path | Duty |
|---|---|
| `src/FusionRpg.Core/Overlay/RiftMenuAnchor.cs` (new) | **Closed surface enum** `RiftMenuSurface { None, MainMenu }` + pure presence carrier: `Set(surface)`, `Clear(surface)`, `Current`, and `IsReviewed`. Unity-free. The enum is a **closed vocabulary** the code owns (see Testing Strategy for the literal-pin rule) |
| `src/FusionRpg.Injector/Hud/MenuPresenceHook.cs` (new) | The Harmony patches: `[HarmonyPatch(typeof(MainMenu), "Start")]` postfix → `RiftMenuAnchor.Set(MainMenu)`; `[HarmonyPatch(typeof(BaseMenu), nameof(BaseMenu.OnHide))]` / `OnExit` postfix **guarded by `__instance is MainMenu`** → `RiftMenuAnchor.Clear(MainMenu)`. Each wrapped so a patch failure logs and cannot break the menu |
| `src/FusionRpg.Injector/Hud/OverlaySwitchGui.cs` · `src/FusionRpg.Injector/Hud/RiftMenuOverlay.cs` | **Not edited by this module** — the consumers are `tombstone`'s |
| `data/tuning/overlay.v1.json` | New `riftMenu` group: the affordance's screen-percentage placement and its device-pixel minimum size |
| `tests/FusionRpg.Core.Tests/Overlay/RiftMenuAnchorTests.cs` (new) | Presence-state semantics: set, clear, type-guarded clear, order-independence |
| `docs/architecture/rift-gate/spec-tombstone.md` | Consumes this signal; the hit-box/never-cover machinery is replaced by uGUI sibling order |

**Host placement (verified).** Both host projects compile `..\FusionRpg.Injector\**\*.cs`
(`FusionRpg.Injector.MelonLoader.39.csproj:33`; `FusionRpg.Injector.BepInEx.csproj`), and the Melon host
additionally defines `FUSIONRPG_MELON` and includes `GlobalUsings.Il2Cpp.cs`. So the patch lives in
shared sources behind the repo's existing host gate, matching `Bridges/pvzrh-3.8.1/CreateZombieHooks.cs:10`:

```csharp
#if FUSIONRPG_MELON
    // MainMenu/BaseMenu resolve only on the Melon host (Il2Cpp.Interop; BepInEx exposes no
    // Assembly-CSharp interop for 3.9 on this machine). BepInEx keeps F10 — see spec-tombstone.
#endif
```

## Code Style

Mirror `OverlaySwitchState`: a pure class, no Unity types, a doc comment that states what it does
**not** do, and a patch class that swallows its own failures.

```csharp
namespace FusionRpg.Core.Overlay;

/// <summary>
/// Which reviewed PVZ menu screen exists right now. Written by two patched LIFECYCLE callbacks and
/// read O(1) by the tombstone's placement path — never sent to the server, never on the hit path,
/// never a combat/domain input, and no navigation method is ever called to produce it.
///
/// Presence, not navigation: Set on the screen's own Start, Clear on its own hide/exit.
/// </summary>
public sealed class RiftMenuAnchor
{
    public RiftMenuSurface Current { get; private set; } = RiftMenuSurface.None;

    public void Set(RiftMenuSurface surface) => Current = surface;

    /// <summary>Clears only the surface that actually went away, so one menu hiding cannot
    /// clear another's presence.</summary>
    public void Clear(RiftMenuSurface surface)
    {
        if (Current == surface) Current = RiftMenuSurface.None;
    }

    public bool IsReviewed => RiftMenuAnchorPolicy.IsReviewed(Current);
}
```

```csharp
// MenuPresenceHook — the whole mechanism is two lifecycle postfixes.
[HarmonyPatch(typeof(MainMenu), "Start")]              // string name: Start is private in 3.9
static class MainMenuStartHook
{
    static void Postfix() => RiftMenuAnchor.Set(RiftMenuSurface.MainMenu);
}

[HarmonyPatch(typeof(BaseMenu), nameof(BaseMenu.OnHide))]   // lifecycle, NOT a nav static
static class MainMenuHiddenHook
{
    static void Postfix(BaseMenu __instance)
    {
        if (__instance is MainMenu) RiftMenuAnchor.Clear(RiftMenuSurface.MainMenu);
    }
}
```

## Testing Strategy

- **Presence semantics are the tested work** (replacing the old six-edge matrix):
  `Start` → `Current == MainMenu`; `OnHide`/`OnExit` on a `MainMenu` → `None`; the same callbacks on a
  **non-`MainMenu`** `BaseMenu` → **unchanged** (the type guard is the load-bearing half — without it,
  any submenu hiding would clear the main-menu signal).
- **Order-independence.** `Set` then `Clear` and the reverse both converge; no ordering is assumed.
- **Closed-enum pin, with its reason.** `RiftMenuSurface` is a **closed vocabulary the code owns and a
  human reviews** — not a derived population — so pinning its literal membership is correct per
  DESIGN-GATE §3.7 and the map's interface rule (`rift-gate-map.md:74-77`), and the test states why.
  No test asserts a count that grows with content or generated data.
- **The forbidden pattern is asserted absent, mechanically.** A guard-style test (source read, matching
  `OverlayPipeContractGuardTests`' style) asserts **no rift-gate source calls a `UIMgr` navigation
  method** — `EnterMainMenu`, `BackToMenu`, `EnterPauseMenu`, `BackToGame` — while allowing the existing
  *observe-only* patches (`GameCaptureHooks.cs:780,790,812`) to remain. This turns the owner's hard rule
  into a red guard instead of a comment.
- **Live (owner terminal):** the signal reads `MainMenu` once the main menu is up; navigating away
  (into a submenu, a match, or the pause menu) clears it; **the game never appears to be driven by us** —
  no board or menu is torn down, no `UIMgr` nav call appears in the log. Read back through the same
  accessor `tombstone` uses (`live-probe-standard.md` §3), not a screenshot alone.
- **Unverified by CI, stated honestly:** Harmony, Il2Cpp interop, and the real menu lifecycle stay
  untested in CI (`overlay-spec.md:154`).

## Boundaries

- **No rift-gate code may call a `UIMgr` navigation method (or any engine navigation action) to change
  what the player sees.** The injector may **read** menu presence and **attach** a UI affordance to an
  existing screen; it may not **drive** the engine's menu state. Switching the WebView is the whole job.
- **Always:** patch the screen's own lifecycle; read names observe-only; clear only the surface that
  went away (type-guarded); keep the enum closed; log every miss and swallow every failure.
- **Ask first:** widening the reviewed allow-list beyond `MainMenu`; patching any additional engine
  lifecycle member; changing the clearance rule.
- **Never:** call `UIMgr.BackToMenu` / `EnterMainMenu` / `EnterPauseMenu` / `BackToGame`; depend on the
  existing observe-only `UIMgr` patches as the detect logic (the map: their detect logic has never
  worked well); send the signal to the server; put it on the hit path; use `Board == null` as the
  signal; include the pause menu; introduce an event-refreshed cache (N/A — no hydration, no key set,
  stated per DESIGN-GATE §2.16).
- **ActorHub gate: N/A.** This module produces and consumes no actor combat/derived/AppliedCombat
  magnitude. It folds no actor number, reads no Hub output, and must not invent one.
- **No tuning number in code** except via the `riftMenu` group; no cap on a magnitude exists here.

## Tunables

| Number | Home | Why |
|---|---|---|
| Affordance placement (screen percentages) | `data/tuning/overlay.v1.json` → `riftMenu` | A feel pass would move it, so it is the balance surface (`tunables-ssot.md` T1). Precedent: `switchLayout` was extracted into the same file (`overlay.v1.json:13-20`), and `RiftMenuOverlayLayout`'s percentages are already `const` at `RiftMenuOverlayLayout.cs:30-33` |
| Affordance device-pixel minimum size + its scale-vs-screen rule | same group | Same class; the derive-from-height precedent is `OverlaySwitchLayout.ScaleFor` (`OverlaySwitchLayout.cs:39-45`) |
| Affordance padding inside its anchor (if any) | same group | Same class |

`data/tuning/overlay.v1.json:7` says *"Never hand-edit this file"* — `tools/tuning/publish.py` **edits
existing tunables only and refuses to invent a key** (`publish.py:130-131`). So the `riftMenu` group is
**authored once by hand** (documented in the commit), after which it is publishable. A missing `riftMenu`
key is a **load rejection naming it** (`OverlayTuningLoader.Parse` reads its groups by name,
`OverlayTuning.cs:47-60`), never a default.

Note: the placement numbers are **shared with the menu art** — `tombstone` places the uGUI affordance
from the same group the art uses, so the visual and the clickable node cannot drift.

## Success Criteria

- [ ] The signal is produced by the **menu screen's own lifecycle** (`MainMenu.Start` + `BaseMenu`'s
      hide/exit), not by any `UIMgr` navigation static.
- [ ] `RiftMenuSurface` is a closed enum with one reviewed member (`MainMenu`) plus `None`; the
      pinned-literal test names the closed-vocabulary reason.
- [ ] Presence reads `MainMenu` after the screen's `Start`; it clears on the screen's hide/exit.
- [ ] A **non-`MainMenu`** `BaseMenu` hiding does **not** clear the signal (type guard tested).
- [ ] **No rift-gate source calls a `UIMgr` navigation method**, asserted by a red guard, not a comment.
- [ ] Task 1 confirms **once** (not as a carried risk) that the 3.9 interop exposes `MainMenu.Start`, that
      the hierarchy paths resolve live, and that the hide/exit callbacks fire; a missed path logs a named
      warning instead of null-derefing.
- [ ] The BepInEx scope is stated as settled-and-secondary: MelonLoader is the first target
      (`deploy-play.ps1:39,115`); BepInEx is checked when that host is touched and keeps F10 if
      `MainMenu` is unavailable there.
- [ ] No per-frame allocation; the signal read is O(1); no server round trip; no `Board == null` signal.
- [ ] ActorHub gate N/A stated; no navigation call; no event-refreshed cache.

## Open Questions

1. **The second reviewed non-gameplay surface.** Decision 11 asks for one beyond the main menu; the code
   proves none safe today. v1 ships the enum with one member; widening is an owner review, not a
   spec-time guess.
2. **`OnHide` vs `OnExit` vs both.** Both are `virtual` on `BaseMenu` and both are lifecycle callbacks,
   so both are patchable; which one (or both) actually fires on each real exit path is confirmed **once**
   by Task 1 against the live menu. The spec patches both, type-guarded, and treats a path that fires
   neither as a real defect with a follow-up — rather than guessing one and being silently stale.
