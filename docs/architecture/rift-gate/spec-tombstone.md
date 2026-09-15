# Spec: `tombstone` (rewritten to map Decision 17, extended by Decision 18)

**Program:** `rift-gate` · **Map:** [../rift-gate-map.md](../rift-gate-map.md) — **Decision 17 + Decision 18 (owner, 2026-09-15)**
**Ideal:** [../rift-gate-ideal.md](../rift-gate-ideal.md) — decisions 11, 15
**Supersedes:** this spec's first draft (an IMGUI rect + a hand-derived axis-aligned hit box + a
"never-cover" policy).
**Depends on:** `menu-anchor` (the presence signal + the placement contract)

> **Decision 17's rule, restated:** **switch the WebView; never fight the PVZ engine.** No rift-gate code
> may call a `UIMgr` navigation method (or any engine navigation action) to change what the player sees.
> This module **attaches** a UI element to a screen that already exists.
>
> **Decision 18's rule:** the menu becomes **one rendering system — uGUI.** The menu art moves on to a
> `Sprite`/`Image` under the menu transform; the in-match "RPG" button (decision 15) **stays IMGUI**
> because it is match HUD chrome, not a menu hierarchy.

---

## Objective

**Attach a real uGUI affordance into the game's own menu hierarchy** on the main menu: click it and the
web FE opens; the game's own canvas, layout and raycast system owns hit-testing and z-order. Plus, per
decision 15, **restyle the existing guard-pinned in-match "RPG" button** as the one live-lawn affordance.

**The transport and the toggle already exist and are not touched.** F10 already opens the overlay from
the main menu in both host modes (`MainWindow.xaml.cs:270` global `RegisterHotKey`; injector
`OverlaySwitch.cs:113-117` + `OverlayViewHost.cs:186`). The affordance is a **discoverability** element,
not a capability fix, and a click must reach the **same** toggle the hotkey and the pipe reach
(`MainWindow.xaml.cs:277` *"Same toggle path as the hotkey — no second behavior"*).

**The reference pattern is the shape to copy** — `darkthemer/PvZF_MainMenuFlowers`, `FlowerClickable.cs`:

1. It patches the menu screen's `Start` (that is `menu-anchor`'s job here) and receives the live
   `MainMenu` instance.
2. It **reads the hierarchy by name**, observe-only: `mainMenu.Find("Grave/GraveBackground/Flower1")`,
   `mainMenu.Find("Grave/LowerButtons")`. Reads only — never an engine action.
3. It **attaches a real uGUI `Button` into the menu's own hierarchy**:
   `image.raycastTarget = true`; a child `GameObject` with a clear `Image` (`raycastTarget = true`) and a
   `RectTransform` stretched (`anchorMin/zero`, `anchorMax/one`, `offsetMin/offsetMax` zero);
   `clickObject.AddComponent<Button>()`; `button.transition = Selectable.Transition.None`;
   `button.targetGraphic = null`; `button.onClick.AddListener(...)`.
4. It **orders itself into the game's own layout** — moving the game's `LowerButtons` **below**
   `GraveBackground` with `SetAsFirstSibling()`, and clearing `raycastTarget` on two text labels
   (`LanguagesButton`, `UpdateInfoButton`) so they do not obstruct. That is how the reference solves
   "do not eat the host's clicks": **sibling order + raycast flags**, not a hand-computed rect.

**What this replaces, and why it is strictly better:**

| First-draft `tombstone` | This spec |
|---|---|
| An IMGUI `GUI.Button` rect drawn *over* the menu | A uGUI `Button` *inside* the menu hierarchy |
| A hand-derived axis-aligned hit box (`RiftMenuOverlayLayout.HitBox`) | **Gone.** The game's own `RectTransform` anchoring + `Image.raycastTarget` own hit-testing |
| Wobble-vs-hitbox (map gap 5): the art is drawn rotated/wobbling (`RiftMenuOverlay.cs:78`) while IMGUI hit-tests axis-aligned, so a separate stable rect was needed | **Gone.** The affordance's own rect is static, and the menu art moves to uGUI too (decision 18) so both share one rendering system |
| "Never cover a host primary action" as a min-size/never-cover policy (gap 7) | Becomes **sibling order**: order ourselves below the game's primary buttons, and clear `raycastTarget` on labels we would obstruct — what the reference does |
| Correcting the over-broad `Board == null` paint gate (gap 4) | Moot **for the menu**: the art and affordance exist only while `MainMenu` exists |
| Six exit edges to clear | **None needed.** The affordance is a child of the menu object, so the menu's own show/hide takes it with it |

## Decision 18 (owner, 2026-09-15) — one rendering system for the menu: uGUI

**The menu art moves into the uGUI hierarchy.** The shipped Rift art is **IMGUI** — `RiftMenuOverlay.cs`
runs inside `OnGUI` (`:20-43`), gates on `Event.current.type == Repaint` (`:25`), and draws with
`GUI.DrawTexture` (`:79`) plus `GUIUtility.RotateAroundPivot` (`:78`). The clickable affordance is
**uGUI**. Decision 18 removes that split for the menu: the art becomes a `Sprite` on an `Image` under the
menu transform — the reference mod's own shape — so there is **one** rendering system for the menu and
the art inherits the game's own canvas scaling.

- **The in-match "RPG" button (decision 15) stays IMGUI.** It is match HUD chrome, not part of a menu
  hierarchy; that split is deliberate and is stated, not an oversight.
- **Option (a) is the documented fallback**, named in the map's enrichment section: if implementation
  finds the IMGUI→uGUI art move is **not small**, keep the painted art and add the uGUI clickable node
  over the same visual position. It is a **considered alternative**, not a silent drift — the spec names
  it so the choice is explicit (see Open Question 1).

## Enrichment — this is a composition of things we already own

The reference pattern looks like a new interaction idiom; **three of its four parts already ship here**.
Verified in this session (`rift-gate-map.md:374-390`):

| Piece | Status | Evidence |
|---|---|---|
| Click a real `UnityEngine.UI.Button` | **Built** | `ControlClick.cs:150-153` — `if (target is UnityEngine.UI.Button button) { button.onClick.Invoke(); }` |
| Census the uGUI hierarchy (`Button`/`Text`/`Image`/`GraphicRaycaster`/`EventSystem`) | **Built** | `ControlInspect.cs:294-298` |
| `UnityEngine.UI` referenced by every host | **Built** | `.Injector.MelonLoader.39.csproj:128` · `.Injector.MelonLoader.csproj:131` · `.Injector.BepInEx.csproj:125` |
| `Sprite` → PNG (inverse direction) | **Built** | `TypeIconCapture.cs:158` `SpriteToPng` |
| PNG file → `Texture2D` | **Built** | `RiftMenuOverlay.cs:112-118` (`ImageConversion.LoadImage`) |
| **`Texture2D` → `Sprite`** | **Real gap (one call)** | No `Sprite.Create` anywhere in `src/FusionRpg.Injector` (verified) — a uGUI `Image` needs a `Sprite` |

**So the honest gap for `tombstone` is `Sprite.Create` + the affordance node**, not a new click or
inspection idiom. `ControlClick` is cited as the precedent for "click a real game Button" so a later
session does not re-derive uGUI plumbing that already exists.

**Success:** on the main menu a click on the tombstone opens the web FE, identical to F10; the affordance
does not steal a single click from the game's own menu buttons; the menu art and the affordance share one
rendering system (uGUI); on a live board and on every other screen neither exists; the in-match button
still carries exactly one action and stays IMGUI.

**ASSUMPTIONS I'M MAKING:**

1. **The affordance can be a plain uGUI `Button` with a listener — no custom `MonoBehaviour`, no
   `ClassInjector`.** The map's own instruction (`rift-gate-map.md:370-372`): prefer that and avoid class
   injection. **Verified: `ClassInjector` / `RegisterTypeInIl2Cpp` / `DelegateSupport` appear nowhere in
   this repo today** (grep over `src/` and `web/`), so introducing them would be a new mechanism for no
   need. The click callback calls `OverlaySwitch.RequestToggle()` (`:44`), which routes to the existing
   pipe `toggle` verb (launcher) or `OverlayViewHost.Toggle()` (injector) — one request path, two sinks.
2. **`button.onClick.AddListener` takes an `Il2CppSystem.Action`/`UnityAction`.** The reference needs
   `DelegateSupport.ConvertDelegate<UnityAction>(callback)` because it captures a lambda; a **static,
   capture-free** method group is the simpler shape and is what this spec prefers — if the interop still
   demands a delegate conversion for a method group, that one conversion is used, but **class injection
   is not**.
3. **The hierarchy object names are confirmed once, live, in Task 1.** `Grave/…`, `LowerButtons`,
   `LanguagesButton`, `UpdateInfoButton` are the reference's paths under **3.8.1**; a scene object's name
   is not in `Assembly-CSharp.dll`, so decompiling cannot settle it. **Per the owner (2026-09-15), the
   menu architecture does not change between versions** — so Task 1 resolves them **once**, and every
   `Find` miss logs a named warning and returns cleanly (never a null-deref, never a silent no-op). A
   missing path degrades to "no affordance" with a log — the honest failure.
4. **The anchor to attach to.** The reference hangs its clickable off an existing decorative child
   (`Flower1`). Our tombstone has no such child, so it needs its **own** anchor under the menu transform.
   Decision 18 supplies it: with the art **also** moving to uGUI, the art node itself is the natural
   anchor (the affordance and the sprite share one node/subtree). Task 1 names the confirmed-live subtree;
   if none resolves, the fallback is a rect-anchored child under the `MainMenu` transform, recorded, not
   silently taken.
5. **Gap 6 decision (unchanged, from the first draft): yes, the button preference still suppresses the
   in-match button while the menu entry stays live — they are different affordances.** But
   `OverlaySwitch.cs:111-117` starts the injector host **only** when `State.SettingsEnabled`, with the
   comment *"with the button off there is no other way to open it in this mode"* — false once the menu
   affordance is independent. The view-start condition must stop keying off the button preference. This
   is a **wiring-gap fix**, owned here.
6. **MelonLoader-first, BepInEx secondary — settled.** MelonLoader is the deployed default
   (`deploy-play.ps1:39,115`); BepInEx is *"the older 3.8.1 install, kept for BepInEx-specific testing"*
   (`:5`). The affordance depends on `menu-anchor`'s presence patch, which is host-gated. If the BepInEx
   interop does not expose `MainMenu` (checked when that host is touched), the tombstone is
   **MelonLoader-only** and BepInEx keeps **F10** — a scoped difference, stated plainly.

→ Correct me now or implementation proceeds with these.

## Tech stack

- Injector C# net6 + Harmony + uGUI. New uGUI construction in the shared injector sources behind the
  existing host gate; `UnityEngine.UI` and `Assembly-CSharp` are already referenced
  (`FusionRpg.Injector.MelonLoader.39.csproj:105-129`).
- **Art load reuse:** both directions of the PNG↔Sprite problem already exist — `RiftMenuOverlay`'s
  `ImageConversion.LoadImage` PNG→`Texture2D` (`:112-118`) and `TypeIconCapture.SpriteToPng`
  (`:158`). The one genuinely new call is **`Sprite.Create`** (Texture2D → Sprite), verified absent from
  `src/FusionRpg.Injector` today.
- **Click reuse:** the shipped `game-control` click path already invokes a real `UnityEngine.UI.Button`
  (`ControlClick.cs:150-153`) — cited as the precedent, not re-derived.
- The click routes through `OverlaySwitch.RequestToggle()` — no new pipe verb, no new toggle path.
- No new packages, no class injection, no new tuning file (the `riftMenu` group is `menu-anchor`'s).

## Commands

```powershell
# Presence + placement (Core; no game, no unity)
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~RiftMenuAnchor"

# The pipe contract guard must stay green (no new verb is added by this module)
dotnet test tests/FusionRpg.Guard.Tests --filter "FullyQualifiedName~OverlayPipeContract"

# Boundary selected for these paths (never the whole suite — AGENTS.md verification boundary)
.\scripts\verify-change.ps1 -Paths src/FusionRpg.Injector/Hud/RiftMenuTombstone.cs,src/FusionRpg.Injector/Hud/OverlaySwitchGui.cs,src/FusionRpg.Injector/Hud/OverlaySwitch.cs -Session rift-gate-spec-20260915-c41a

# Injector compiles with real interop (the part CI cannot build)
dotnet build src/FusionRpg.Injector.MelonLoader.39 -c Release -p:MlGameDir=$env:FUSIONRPG_ML_GAMEDIR -p:GameProfile=pvzrh-3.9
```

## Project structure

| Path | Duty |
|---|---|
| `src/FusionRpg.Injector/Hud/RiftMenuTombstone.cs` (new) | The uGUI affordance **and the art node** (decision 18). Called from the `menu-anchor` presence patch: resolves the live `MainMenu`, finds the anchor subtree, builds a child `GameObject` with a stretched `RectTransform`, an `Image` whose `sprite` is the Rift art (`Sprite.Create` — the one new call) and `raycastTarget = true`, and a `Button` (`transition = None`, `targetGraphic = null`) whose listener calls `OverlaySwitch.RequestToggle()`. Orders itself below the game's primary buttons and clears `raycastTarget` on labels it would obstruct. Every `Find` miss → named warning + clean return |
| `src/FusionRpg.Injector/Hud/RiftMenuArt.cs` (new, small) | PNG → `Texture2D` (reusing `ImageConversion.LoadImage`, `RiftMenuOverlay.cs:112-118`) → **`Sprite.Create`** (the verified new call), cached. The single place the art becomes a sprite |
| `src/FusionRpg.Injector/Hud/MenuPresenceHook.cs` (from `menu-anchor`) | Owns the lifecycle patches; the affordance + art node are **built** here (on `MainMenu.Start`) and need **no** teardown patch of their own |
| `src/FusionRpg.Injector/Hud/RiftMenuOverlay.cs` | **Retired for the menu** (decision 18): its IMGUI `OnGUI`/`GUI.DrawTexture` draw path (`:20-43,:79`) goes away, replaced by the uGUI `Image`. Its PNG loader (`:97-133`) moves to `RiftMenuArt`. **The file itself is deleted only if the paint has no other consumer** — Task 4 confirms (the art's `RiftMenuOverlayLayout` percentages stay in Core) |
| `src/FusionRpg.Injector/Hud/OverlaySwitchGui.cs` | **Restyle only** (decision 15): the in-match `GUI.Button(_rect, "RPG")` (`:40`) becomes the tombstone treatment. Its single action (`OverlaySwitch.RequestToggle()`, `:41`) and gate (`OverlaySwitch.ButtonVisible`, `:23`) are unchanged. **Stays IMGUI** (decision 18's deliberate split) |
| `src/FusionRpg.Injector/Hud/OverlaySwitch.cs` | Gap 6: the injector-host view-start condition (`:111-117`) stops keying off the **button** preference once the menu affordance is an independent entry |
| `src/FusionRpg.Core/Overlay/RiftMenuOverlayLayout.cs` | **No hit-box addition.** Its existing percentages (via the `riftMenu` group) place the art node and the affordance, so visual and clickable node cannot drift; the first draft's `HitBox`/`MinimumHitDevicePx` additions are **dropped** |
| `data/tuning/overlay.v1.json` | `riftMenu` group (owned by `menu-anchor`; read here) |
| `src/FusionRpg.Injector.BepInEx/Plugin.cs` · `.../MelonLoader/MelonFusionRpgMod.cs` | **No new line.** The first draft added an IMGUI draw call per host; a uGUI art node + affordance are built once from the presence patch, not drawn per frame. (Both hosts' existing `RiftMenuOverlay.Draw()` call sites are removed with the IMGUI path.) |

## Code Style

Match the reference's construction order and the repo's existing "swallow and log, never throw into the
game loop" discipline. This is the whole mechanism, in the reference's own shape, with decision 18's art
node in the same subtree:

```csharp
/// <summary>
/// The Rift art and its clickable tombstone, inside the game's own menu hierarchy (decision 18: ONE
/// rendering system for the menu — uGUI, not IMGUI). One action: open/close the web FE through the same
/// request path the in-match button and F10 use. It is not a cheats surface.
///
/// Decision 17: this ATTACHES a UI element to a screen that already exists. It never calls a UIMgr
/// navigation method and never drives the engine's menu state.
/// </summary>
static void Attach(Transform menu)
{
    var anchor = menu.Find(RiftMenuAnchorPath);          // path confirmed once, live, in Task 1
    if (anchor == null) { RpgHost.Log.Warning($"[rift] menu anchor '{RiftMenuAnchorPath}' not found; tombstone disabled"); return; }

    var go = new GameObject("FusionRpgRiftTombstone");
    go.transform.SetParent(anchor, false);

    var rect = go.AddComponent<RectTransform>();
    rect.anchorMin = Vector2.zero;                        // stretched to its anchor, as the reference does
    rect.anchorMax = Vector2.one;
    rect.offsetMin = Vector2.zero;
    rect.offsetMax = Vector2.zero;

    var image = go.AddComponent<Image>();
    image.sprite = RiftMenuArt.Sprite();                  // Sprite.Create — the one new call
    image.preserveAspect = true;
    image.raycastTarget = true;                           // the game's own canvas raycasts for us

    var button = go.AddComponent<Button>();
    button.transition = Selectable.Transition.None;
    button.targetGraphic = null;
    button.onClick.AddListener(OnClick);

    OrderBelowPrimaryButtons(menu);                       // sibling order + label raycast flags
}

static void OnClick() => OverlaySwitch.RequestToggle();   // the SAME path the button and F10 use
```

## Testing Strategy

- **The hit-box machinery is deleted, so its tests are deleted.** `HitBox` /
  `MinimumHitDevicePx` / the wobble-independence test from the first draft are **removed with the
  mechanism** (the game's `RectTransform` + raycast owns hit-testing). No test asserts a rect we no
  longer compute. `RiftMenuOverlayLayout`'s existing tests stay, unchanged.
- **`Sprite.Create` is the one new call, and it is unit-tested where it can be.** The PNG→`Texture2D`
  half already ships (`ImageConversion.LoadImage`, `RiftMenuOverlay.cs:112-118`); the Texture2D→`Sprite`
  half is a pure function on a loaded texture and is tested for a null/undecodable PNG degrading to a
  named warning (no affordance, not a throw). **No test asserts a population count.**
- **Placement is still pure and tested.** The node's placement comes from the `riftMenu` group through a
  Core function, so its structural bounds (on-screen, positive, at `(0,0)`/720p/1080p/4K) are tested in
  `FusionRpg.Core.Tests` exactly as `OverlaySwitchLayout` is.
- **"Order below the host's primary buttons" is asserted as a contract.** A test pins the ordering call
  and the label `raycastTarget` clears (source-read style, matching `OverlayPipeContractGuardTests`), so
  the "we did not eat a host click" property is guarded rather than eyeballed. Live confirmation is a
  click on a real menu button with the tombstone present.
- **Decision 18 is asserted as a contract, not left to drift.** A source-read test asserts the **menu**
  path no longer paints via IMGUI (`GUI.DrawTexture` / `GUIUtility.RotateAroundPivot` only inside the
  in-match `OverlaySwitchGui` path) while the **in-match RPG button keeps its IMGUI** — the deliberate
  split stated in decision 18. If the option-(a) fallback is taken instead, that test asserts the
  documented fallback shape and the spec records why.
- **The forbidden pattern is asserted absent** (shared with `menu-anchor`'s guard): no rift-gate source
  calls a `UIMgr` navigation method.
- **Pipe contract unchanged.** `OverlayPipeContractGuardTests` (`:39-57`) must stay green **untouched** —
  a red there means the click grew its own path, which is the defect.
- **The single-action identity is preserved, and the guard is updated with it.** No automated literal pin
  on the `"RPG"` string exists today (grep confirms; the pin is the doc at `OverlaySwitchGui.cs:7` plus
  behaviour), so the work is: keep `OverlaySwitch.RequestToggle()` as the button's only call, and add a
  test asserting the restyled control still has exactly one action. If a literal pin is added during
  implementation, it moves with the label in the same commit.
- **Live (owner terminal) — the mandatory checks:**
  1. the tombstone is **visible** on the main menu and **absent** in a match, on the pause menu, and on
     every other screen (it is a child of the main menu only);
  2. clicking it opens the web FE, identical to F10;
  3. clicking a **real menu button beside it** still works (we did not steal a click);
  4. the menu art renders in the **game's own canvas** (it scales with the menu, and no IMGUI draw of the
     menu art happens) — decision 18;
  5. **no `UIMgr` navigation call appears in the log and no board/menu is torn down**.
  Read back through the normal path (`live-probe-standard.md` §3): the overlay actually covering the game
  and the menu actually still usable — not a screenshot of a click.
- **Unverified by CI, stated honestly:** Win32, WebView2, Harmony, Il2Cpp interop, and uGUI raycast
  behaviour stay untested in CI (`overlay-spec.md:154`).

## Boundaries

- **No rift-gate code may call a `UIMgr` navigation method (or any engine navigation action) to change
  what the player sees.** The injector may **read** menu presence and **attach** a UI affordance to an
  existing screen; it may not **drive** the engine's menu state. Switching the WebView is the whole job.
- **Always:** build the art + affordance as **children of the menu hierarchy** (one uGUI rendering system
  for the menu — decision 18); route the click through `OverlaySwitch.RequestToggle()`; keep the in-match
  button's single action and its IMGUI path; keep every failure swallowed and logged; clear
  `raycastTarget` on anything we would obstruct; order ourselves below the host's primary buttons.
- **Ask first:** adding a **second** action to the in-match button; introducing `ClassInjector` / a custom
  `MonoBehaviour`; attaching the affordance anywhere but the reviewed main-menu screen; abandoning the
  IMGUI→uGUI art move for fallback (a) **without recording why**.
- **Never:** call a `UIMgr` navigation method; a second toggle path or a second pipe verb; a cheats
  surface on the tombstone; leaving the menu with **two** rendering systems (decision 18 forbids a
  new painting of the menu art beside the uGUI one); converting the in-match RPG button to uGUI (it stays
  IMGUI by decision); a new canvas; a new draw line in either host's `OnGUI`; binding F10 in the FE
  (`keymap.ts:18` forbids it, `keymapGuard.test.ts:10-11` asserts it).
- **ActorHub gate: N/A.** This module produces and consumes no actor combat/derived/AppliedCombat
  magnitude. It folds no actor number and reads no Hub output.
- **No runtime code in this phase** (spec phase); this section governs the build that follows.

## Tunables

| Number | Home | Why |
|---|---|---|
| Affordance placement (screen percentages) | `data/tuning/overlay.v1.json` → `riftMenu` (**owned by `menu-anchor`**, read here) | A feel pass would move it; `RiftMenuOverlayLayout.cs:30-33` holds the art's percentages and the affordance reads the same group so art and target cannot drift |
| Affordance device-pixel minimum size | same group | Same class |
| In-match button geometry | `overlay.v1.json` → `switchLayout` (already exists, `:13-20`) | Unchanged by the restyle — presentation, not geometry |

A missing `riftMenu` key is a **load rejection naming it** (`OverlayTuning.cs:47-60`), never a default.
`publish.py` refuses to invent a key (`publish.py:130-131`), so the group is authored once by hand in the
`menu-anchor` commit.

## Success Criteria

- [ ] A click on the main-menu tombstone opens the web FE, identical to F10 in both host modes.
- [ ] The affordance is a **uGUI `Button` inside the menu hierarchy** — no IMGUI rect, no hand-derived
      hit box, no `HitBox`/`MinimumHitDevicePx` additions to `RiftMenuOverlayLayout`.
- [ ] **Decision 18:** the menu art renders as a `Sprite` on a uGUI `Image` under the menu transform; the
      menu has **one** rendering system; `Sprite.Create` is the only new art call.
- [ ] **Decision 18's deliberate split holds:** the in-match "RPG" button **stays IMGUI** (match HUD
      chrome, not a menu hierarchy) — asserted by a guard, not left to drift.
- [ ] The IMGUI menu draw path is retired for the menu (its `OnGUI`/`GUI.DrawTexture` call sites gone from
      both hosts); no new `OnGUI` line is added anywhere.
- [ ] The click routes through `OverlaySwitch.RequestToggle()`; no second toggle path, no new pipe verb
      (`OverlayPipeContractGuardTests` untouched and green).
- [ ] The affordance exists **only** on the main menu screen; absent on the pause menu, a live board, and
      every other screen (it is a child of the menu object).
- [ ] **A real menu button beside it still receives its click** (sibling order + label `raycastTarget`
      clears), confirmed live.
- [ ] The hierarchy anchor path is confirmed **once, live**, in 3.9 (Task 1); a miss logs a **named
      warning** and disables the tombstone cleanly instead of null-derefing.
- [ ] The in-match "RPG" button keeps exactly one action (show/hide); the restyle is presentation only.
- [ ] Gap 6 fixed: with the button preference off in injector mode, the menu affordance still opens the
      view; the preference suppresses only the in-match button.
- [ ] **MelonLoader-first:** the tombstone ships on the deployed default; BepInEx keeps **F10** if its
      interop lacks `MainMenu` — stated, not assumed.
- [ ] No `ClassInjector` / custom `MonoBehaviour` introduced; no `UIMgr` navigation call anywhere.
- [ ] If fallback (a) is taken (paint kept + uGUI node over it), the spec records **why** and the guard
      asserts the documented fallback shape.
- [ ] ActorHub gate N/A stated; no server round trip; no `data/tuning` number beyond the `riftMenu` read.

## Open Questions

1. **Fallback (a) vs decision 18's move (b).** (b) is the owner's decision and the cleaner end state (one
   rendering system, art inherits the game's canvas scaling). (a) — keep the painted art, add the uGUI
   node over the same visual position — is the **documented fallback** if the IMGUI→uGUI move proves not
   small. This spec takes **(b)** and names (a) so the choice is explicit; if implementation switches to
   (a), it records why and updates the guard. It is not a silent drift.
2. **The confirm-live anchor path in 3.9** (assumption 3/4). Task 1's one-time check; the fallback (a
   rect-anchored child under the `MainMenu` transform) is recorded so a miss is a scoped decision.
3. **Does the retired `RiftMenuOverlay.cs` have any other consumer?** Task 4 confirms; if the IMGUI paint
   has no other call site, the file (and its two host `Draw()` lines) go. If something else uses it, only
   the menu usage is removed and the file stays. Recorded so a deletion is deliberate.
4. **The second reviewed surface** (map decision 11) is `menu-anchor`'s open question; the tombstone
   inherits whatever the enum grows to, with no code change beyond the shared presence patch.
