# Todo: `rift-gate`

**Plan:** [rift-gate-plan.md](rift-gate-plan.md) · **Map:**
[../docs/architecture/rift-gate-map.md](../docs/architecture/rift-gate-map.md) ·
**Specs:** `docs/architecture/rift-gate/spec-*.md`

> **Read the map's Audit section *and* Decision 17 first.** Its load-bearing points:
> `overlay-hide` relays through the **existing** server→injector command path; `first-open-signal` is
> written **FE/server-side** (the injector cannot see the launcher's F10, and the player row already
> exists); and **`menu-anchor` is a menu-screen _presence_ signal (Decision 17)** — patch the screen's
> own lifecycle, **never** a `UIMgr` navigation static. The rule: **switch the WebView; never fight the
> PVZ engine.** `UIMgr.BackToMenu`/`EnterMainMenu` are forbidden (live hazard, `DebugActions.cs:1495-1496`);
> calling them mid-run breaks a live run.

## Standing verification (every code task — T1–T13)

- [ ] `.\scripts\verify-change.ps1 -Paths <changed files> -Session <build-session-id>` selects the
      focused checks (required local workflow — never a broad suite from caution)
- [ ] Injector tasks (T2–T6): `dotnet build src/FusionRpg.Injector.MelonLoader.39 -c Release -p:MlGameDir=$env:FUSIONRPG_ML_GAMEDIR -p:GameProfile=pvzrh-3.9` green — CI cannot build the interop
- [ ] Const/tuning-introducing tasks: `python scripts/audit-magic-numbers.py` shows no new M1/M2
      (structural consts carry their T2 comment)
- [ ] Web tasks: `npm run build` (tsc --noEmit) + `npm run check:bundle` green
- [ ] Every acceptance asserts a **contract**, never a derived population count; a closed enum may pin a
      literal **with its reason**
- [ ] Spec updated before code on any discovery (specs are living documents)

---

## Slice 1 — `menu-anchor` (prerequisite for `tombstone`)

### Task 1: 3.9 interop verification — the one-time check, before any patch

**Description:** Confirm against the live 3.9 interop what the spec session already verified by
decompiling, and settle the remaining live item. Per the owner, *no one redesigns a main-menu hierarchy
between versions*, so this is **one check**, not a risk to carry. Confirm: `Il2Cpp.MainMenu : BaseMenu`
exists; `Start` is the entry method (and is **private**, so it must be patched by **string name**);
`OnHide`/`OnExit` are `virtual` on `BaseMenu` and are the clearance callbacks (there is **no**
`OnDestroy`); and the live main-menu hierarchy paths (`Grave/…`, `LowerButtons`, `LanguagesButton`,
`UpdateInfoButton`) using the reference's own `Find` calls. Record every finding in the spec.

**Acceptance criteria:**

- [ ] `Il2Cpp.MainMenu : BaseMenu` confirmed in the 3.9 interop; `Start` confirmed (private).
- [ ] `BaseMenu`’s `OnHide`/`OnExit` confirmed `virtual`; **no** `OnDestroy` relied upon.
- [ ] Each hierarchy path is confirmed **live** (or recorded as a named miss with its logged warning).
- [ ] The BepInEx scope is recorded as **settled and secondary**: MelonLoader is the deployed default
      (`deploy-play.ps1:39,115`); if BepInEx interop lacks `MainMenu` when that host is touched, the
      affordance is Melon-only and BepInEx keeps **F10** — stated, not treated as a design input.

**Verification:**

- [ ] `ilspycmd -t Il2Cpp.MainMenu "$env:FUSIONRPG_ML_GAMEDIR\MelonLoader\Il2CppAssemblies\Assembly-CSharp.dll"`
- [ ] `ilspycmd -t Il2Cpp.BaseMenu …` (lifecycle members)
- [ ] Live (owner terminal): the main menu is observed and the path names are read off the real scene

**Dependencies:** None (map + Decision 17/18 approved).

**Files likely touched:**

- `docs/architecture/rift-gate/spec-menu-anchor.md` (record the findings)

**Estimated scope:** S.

### Task 2: Presence signal — closed enum + pure carrier + the **menu-screen lifecycle** patch

**Description:** New `src/FusionRpg.Core/Overlay/RiftMenuAnchor.cs` — `RiftMenuSurface { None, MainMenu }`
(closed enum the code owns and a human reviews) and a pure presence carrier
(`Set`/`Clear`/`Current`/`IsReviewed`), Unity-free. New `src/FusionRpg.Injector/Hud/MenuPresenceHook.cs`
with the Harmony patches: `[HarmonyPatch(typeof(MainMenu), "Start")]` (string name — `Start` is private
in 3.9) → `Set(MainMenu)`; `[HarmonyPatch(typeof(BaseMenu), nameof(BaseMenu.OnHide))]` and `OnExit`
postfixes, **guarded by `__instance is MainMenu`** → `Clear(MainMenu)`. Host-gated with
`#if FUSIONRPG_MELON` (the type resolves only on the Melon host; see Task 1). The pause menu is excluded
**by name** (it is not a `MainMenu`, so the guard cannot set it; it sits over a live board —
`OverlaySwitchState.cs:44-48`).

**Acceptance criteria:**

- [ ] `RiftMenuSurface` is closed; its pinned-literal test states the closed-vocabulary reason.
- [ ] The signal is set by `MainMenu.Start` and cleared by `BaseMenu`’s hide/exit — **no `UIMgr`
      navigation method is patched or called anywhere**.
- [ ] The **type guard** is tested: a non-`MainMenu` `BaseMenu` hiding does **not** clear the signal.
- [ ] The patch is host-gated (`FUSIONRPG_MELON`) and a patch failure logs rather than escaping.
- [ ] No per-frame allocation; the signal read is O(1).

**Verification:**

- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~RiftMenuAnchor"` green
- [ ] `.\scripts\verify-change.ps1 -Paths src/FusionRpg.Core/Overlay/RiftMenuAnchor.cs,src/FusionRpg.Injector/Hud/MenuPresenceHook.cs -Session <id>`
- [ ] Injector compiles with interop (MelonLoader.39 build)

**Dependencies:** Task 1.

**Files likely touched:**

- `src/FusionRpg.Core/Overlay/RiftMenuAnchor.cs` (new)
- `src/FusionRpg.Injector/Hud/MenuPresenceHook.cs` (new)

**Estimated scope:** M.

### Task 3: Presence semantics tests + the **no-navigation red guard** + the `riftMenu` tuning group

**Description:** The presence test matrix: `Start` → `MainMenu`; `OnHide`/`OnExit` on a `MainMenu` →
`None`; the same callbacks on a **non-`MainMenu`** `BaseMenu` → **unchanged**; order-independence both
ways. Add a **guard-style test** (source-read, matching `OverlayPipeContractGuardTests`’ style) asserting
**no rift-gate source calls a `UIMgr` navigation method** (`EnterMainMenu`, `BackToMenu`,
`EnterPauseMenu`, `BackToGame`) while allowing the existing *observe-only* patches
(`GameCaptureHooks.cs:780,790,812`) to remain. Author the `riftMenu` group in
`data/tuning/overlay.v1.json` **by hand once** (the placement percentages + device-pixel minimum size);
`OverlayTuningLoader.Parse` gains the group and **rejects by name** when absent (mirroring
`switchLayout`, `OverlayTuning.cs:47-60`).

**Acceptance criteria:**

- [ ] Each presence path has its **own** test; the non-`MainMenu` case is explicitly covered.
- [ ] **The no-navigation guard is green** — and goes red if a `UIMgr` nav call is introduced (prove it
      by a temporary local check, then revert).
- [ ] A missing `riftMenu` key is a **load rejection naming it**, never a default.
- [ ] No test asserts a population count; these are structural bounds.
- [ ] The first draft’s `HitBox`/`MinimumHitDevicePx` additions are **not** present in
      `RiftMenuOverlayLayout`.

**Verification:**

- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~RiftMenuAnchor"` green
- [ ] `dotnet test tests/FusionRpg.Guard.Tests` green (the new no-navigation guard)
- [ ] `python scripts/audit-magic-numbers.py` — no new M1/M2 (the extracted numbers carry their comment)
- [ ] `dotnet run` a tuning load with the group removed → rejection names `riftMenu`

**Dependencies:** Task 2.

**Files likely touched:**

- `tests/FusionRpg.Core.Tests/Overlay/RiftMenuAnchorTests.cs` (new)
- `tests/FusionRpg.Guard.Tests/` (the no-navigation guard, new)
- `src/FusionRpg.Core/Overlay/OverlayTuning.cs`
- `data/tuning/overlay.v1.json`

**Estimated scope:** M.

### Checkpoint A

- [ ] The signal reads `MainMenu` after the screen’s `Start`; clears on the screen’s own hide/exit.
- [ ] A **non-`MainMenu`** `BaseMenu` hiding does **not** clear it.
- [ ] **No rift-gate source calls a `UIMgr` navigation method** (red guard proven).
- [ ] `riftMenu` group loads, or rejects by name when missing.
- [ ] The 3.9 hierarchy paths are confirmed live (or a named miss is recorded).

---

## Slice 2a — `tombstone` (needs slice 1)

### Task 4: Attach a uGUI affordance + move the menu art to uGUI — Decisions 17 & 18

**Description:** Two coupled changes, one rendering system.

**(17) The affordance.** New `src/FusionRpg.Injector/Hud/RiftMenuTombstone.cs`, built from the
`menu-anchor` presence patch: resolve the live `MainMenu`, `Find` the confirmed-live anchor subtree, and
attach a child `GameObject` with a **stretched** `RectTransform` (`anchorMin` zero / `anchorMax` one /
offsets zero), an `Image` with `raycastTarget = true`, and a `Button`
(`transition = Selectable.Transition.None`, `targetGraphic = null`) whose listener calls
`OverlaySwitch.RequestToggle()` (`:44`) — the **same** path the in-match button and the pipe use. Order
below the game's primary buttons (`SetAsFirstSibling`) and clear `raycastTarget` on labels we would
obstruct. **No IMGUI rect, no hand-derived hit box, no `ClassInjector`.** Every `Find` miss logs a
**named warning** and disables the tombstone cleanly. Cite `ControlClick.cs:150-153` as the precedent for
clicking a real game Button — do not re-derive it.

**(18) The art.** New `src/FusionRpg.Injector/Hud/RiftMenuArt.cs`: PNG → `Texture2D` (reusing
`ImageConversion.LoadImage`, `RiftMenuOverlay.cs:112-118`) → **`Sprite.Create`** (the verified new call)
→ cached `Sprite` on the art node's `Image`. **Retire the IMGUI menu draw path**: remove `RiftMenuOverlay`'s
`OnGUI` menu draw and its two host `Draw()` call sites. The in-match **RPG button stays IMGUI**
(decision 15 + 18's deliberate split). If the move proves not small, fallback **(a)** — keep the paint,
add the uGUI node over the same position — is the documented alternative and the spec records why.

**Acceptance criteria:**

- [ ] The affordance is a real uGUI `Button` **inside** the menu hierarchy — the game's own layout and
      raycast own hit-testing; no `HitBox`/`MinimumHitDevicePx` additions to `RiftMenuOverlayLayout`.
- [ ] The menu art is a `Sprite` on a uGUI `Image` under the menu transform; the menu has **one**
      rendering system; `Sprite.Create` is the only new art call.
- [ ] A main-menu click opens the FE, identical to F10; the click routes through
      `OverlaySwitch.RequestToggle()`; **no** new pipe verb.
- [ ] **A real menu button beside it still receives its click** (sibling order + `raycastTarget` clears).
- [ ] The affordance exists **only** on the main menu; absent on the pause menu, a live board, and every
      other screen.
- [ ] The IMGUI menu draw sites are removed from both hosts; **no** new `OnGUI` line is added; the
      in-match RPG button keeps its IMGUI path (guard-asserted, not left to drift).
- [ ] No `ClassInjector` / custom `MonoBehaviour`; a `Find` miss or an undecodable PNG logs a named
      warning and degrades cleanly.
- [ ] MelonLoader-first scope stated: BepInEx keeps **F10** if its interop lacks `MainMenu`.

**Verification:**

- [ ] `dotnet test tests/FusionRpg.Guard.Tests --filter "FullyQualifiedName~OverlayPipeContract"` green **untouched**
- [ ] `.\scripts\verify-change.ps1 -Paths src/FusionRpg.Injector/Hud/RiftMenuTombstone.cs,src/FusionRpg.Injector/Hud/RiftMenuArt.cs,src/FusionRpg.Injector/Hud/RiftMenuOverlay.cs -Session <id>`
- [ ] Live (owner terminal): tombstone visible on the main menu; absent in a match and on the pause menu;
      click opens the overlay; a neighbouring menu button still works; the menu art scales with the game's
      canvas (no IMGUI menu draw); **no `UIMgr` nav call in the log**

**Dependencies:** Task 1–3 (presence signal + anchor paths confirmed).

**Files likely touched:**

- `src/FusionRpg.Injector/Hud/RiftMenuTombstone.cs` (new)
- `src/FusionRpg.Injector/Hud/RiftMenuArt.cs` (new)
- `src/FusionRpg.Injector/Hud/RiftMenuOverlay.cs` (retire the IMGUI menu draw; move the loader)
- `src/FusionRpg.Injector/Hud/MenuPresenceHook.cs` (calls attach on `Start`)
- `src/FusionRpg.Injector.BepInEx/Plugin.cs` · `.../MelonLoader/MelonFusionRpgMod.cs` (remove the menu-art `Draw()` line)

**Estimated scope:** M/L.

### Task 5: Gap 6 — the injector-host view start stops keying off the button preference

**Description:** `OverlaySwitch.cs:111-117` starts the view only when `State.SettingsEnabled`, with the
comment *"with the button off there is no other way to open it in this mode"* — false once the menu
tombstone is independent. Fix the wiring: the view starts so the menu entry works; the preference
suppresses only the **in-match button** (`OverlaySwitchState.ButtonVisible`, `:51`). Decision (gap 6):
**yes, the preference still suppresses the in-match button while the menu entry stays live** — they are
different affordances.

**Acceptance criteria:**

- [ ] Injector mode with the button preference off: the menu tombstone still opens the view.
- [ ] The in-match button is still hidden when the preference is off.
- [ ] No second start path; `_viewStarted` still guards a single start.

**Verification:**

- [ ] `.\scripts\verify-change.ps1 -Paths src/FusionRpg.Injector/Hud/OverlaySwitch.cs -Session <id>`
- [ ] Live (owner terminal, `FUSIONRPG_OVERLAY_HOST=injector`, button preference off): the menu entry
      opens the view; the in-match button is absent

**Dependencies:** Task 4 (the menu entry must exist to be the independent path).

**Files likely touched:**

- `src/FusionRpg.Injector/Hud/OverlaySwitch.cs`

**Estimated scope:** S.

### Task 6: The in-match "RPG" button restyle (decision 15) — one affordance, one action

**Description:** Restyle `OverlaySwitchGui` (`:40`, `GUI.Button(_rect, "RPG")`) to the tombstone
treatment. **Presentation only**: keep `OverlaySwitch.RequestToggle()` (`:41`) as its single call and
`OverlaySwitch.ButtonVisible` (`:23`) as its gate. The guard/test that pins its single-action identity
is **updated, not bypassed** — the button still carries one action (show/hide). No literal pin on the
`"RPG"` string exists today (grep confirms; the pin is the doc at `:7` plus behaviour), so the work is:
keep one action, and add a test asserting the restyled control has exactly one action.

**Acceptance criteria:**

- [ ] The button is restyled; its **single action** (show/hide) and gate are unchanged.
- [ ] A test asserts the restyled control has exactly one action (one request call).
- [ ] No second lawn affordance is introduced.
- [ ] If any literal label pin is added during implementation, it moves with the label in the same commit.

**Verification:**

- [ ] `.\scripts\verify-change.ps1 -Paths src/FusionRpg.Injector/Hud/OverlaySwitchGui.cs -Session <id>`
- [ ] Live (owner terminal): the restyled button toggles once, and only once (debounce holds)

**Dependencies:** Task 4 (same menu/lawn affordance work; land together).

**Files likely touched:**

- `src/FusionRpg.Injector/Hud/OverlaySwitchGui.cs`
- `tests/FusionRpg.Core.Tests/Overlay/OverlaySwitchStateTests.cs` (only if a pin moves)

**Estimated scope:** S.

### Checkpoint B

- [ ] Main-menu click opens the FE, identical to F10.
- [ ] No hit target on any non-reviewed surface (pause, boards, every other screen).
- [ ] The menu has **one** rendering system (uGUI art + uGUI affordance); the in-match RPG button keeps
      IMGUI (decision 18's deliberate split, guard-asserted).
- [ ] Pipe guard green **untouched** (no new verb); the in-match button still one action.
- [ ] Gap 6 fixed: injector mode + preference off → menu entry still opens the view.

---

## Slice 2b — `overlay-hide` (parallel with 2a)

### Task 7: The named vocabulary constant + the FE→host close path

**Description:** New `src/FusionRpg.Core/Overlay/OverlayCommandNames.cs` with
`public const string Hide = "overlay.hide";` — **not** a bare literal in the drain. The drain's name is a
misnomer (it already runs seven non-cheat refresh commands, `CheatCommandRunner.cs:57-105`);
`CheatCommandRunner` is **not** renamed. `CheatCommandRunner` gains one drain case: injector-hosted →
`OverlayViewHost.Hide()` (`:67`), launcher-hosted → `OverlaySwitch` sends the pipe `hide` verb.

**Acceptance criteria:**

- [ ] The command name is the Core constant; no bare literal in the drain.
- [ ] `CheatCommandRunner` is not renamed; existing callers untouched.
- [ ] Injector-hosted hide calls `OverlayViewHost.Hide()` in-process.
- [ ] **Hide writes no story ledger** — the drain case never reaches any onboarding write.

**Verification:**

- [ ] `dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Overlay"` green
- [ ] `.\scripts\verify-change.ps1 -Paths src/FusionRpg.Core/Overlay/OverlayCommandNames.cs,src/FusionRpg.Injector/CheatCommandRunner.cs -Session <id>`

**Dependencies:** None (independent of slice 1).

**Files likely touched:**

- `src/FusionRpg.Core/Overlay/OverlayCommandNames.cs` (new)
- `src/FusionRpg.Injector/CheatCommandRunner.cs` (one drain case)
- `src/FusionRpg.Injector/Hud/OverlaySwitch.cs` (the pipe send / local hide split)

**Estimated scope:** M.

### Task 8: The server endpoint — via the **existing** `SendInjectorCommand`, no third copy

**Description:** One endpoint (`POST /api/overlay/leave`) on the existing server that builds
`new CommandDto { Name = OverlayCommandNames.Hide }` and sends via the **existing** helper
(`Program.cs:1617`). The helper exists **twice** (`Program.cs:1617`, `UniqueActorService.cs:281`);
**pick `Program.cs`'s** and say why — the close request is not a unique-actor domain operation.
**Adding a third is forbidden.**

**Acceptance criteria:**

- [ ] One endpoint; the command is built via the existing `Program.cs` helper.
- [ ] **No third** `SendInjectorCommand` copy exists after the change (**two**, not three).
- [ ] The endpoint never waits on the effect; hiding an already-hidden view is a no-op.

**Verification:**

- [ ] `dotnet test tests/FusionRpg.Server.Tests` green
- [ ] `.\scripts\verify-change.ps1 -Paths src/FusionRpg.Server/Program.cs -Session <id>`
- [ ] `rg "Task SendInjectorCommand\(" src/` → exactly two definitions

**Dependencies:** Task 7 (the constant).

**Files likely touched:**

- `src/FusionRpg.Server/Program.cs` (one endpoint)

**Estimated scope:** S.

### Task 9: The story-ledger contract — hide never acknowledges, tested at both ends

**Description:** The hard contract. `RiftPrologueDialog.tsx:131-133` treats `onOpenChange(false)` /
`onEscapeKeyDown` as a durable `completed | skipped` write via `finish(...)` (`:101-120`). Hide must be
separate: close the window, touch no ledger. Prove it at the FE (a leave/hide is **not** an ack
mutation) and at the Server (the leave endpoint writes no `rpg_onboarding_story` change), reading back
through the normal onboarding path (`OnboardingEndpoints.ProjectState`).

**Acceptance criteria:**

- [ ] A leave/hide does **not** call `acknowledge.mutateAsync`; the story resumes at beat 1 next open.
- [ ] The server path writes no `rpg_onboarding_story` row/change — read back through the normal path.
- [ ] The dialog's own Skip/complete still acknowledges exactly as before (unchanged).

**Verification:**

- [ ] `cd web/fusion-rpg-web; npm test -- --run RiftPrologue` green, plus a new no-ack-on-hide case
- [ ] `dotnet test tests/FusionRpg.Data.Tests --filter "FullyQualifiedName~Onboarding"` green

**Dependencies:** Task 7 (the hide path), Task 8 (the endpoint).

**Files likely touched:**

- `web/fusion-rpg-web/src/features/onboarding/RiftPrologueDialog.tsx` (separate hide from ack)
- `web/fusion-rpg-web/src/shell/OverlayLeave.tsx` (new)

**Estimated scope:** M.

### Task 10: The pipe `hide` verb + the embed marker in both hosts + the Launcher route

**Description:** `OverlayPipeCommand.Hide` + the `"hide" => OverlayPipeCommand.Hide` row
(`OverlayPipeServer.cs:81-86`); `OnOverlayPipeCommand` gains a `Hide` case calling
`HideOverlayToGame()` (`MainWindow.xaml.cs:286-299`) — the **same** hide Esc uses (`:354`). Both hosts
append the **embed marker** at navigate time (`OverlayViewHost.cs:275-287`;
`OverlayWindow.xaml.cs:85-89`). Verified same-origin-safe (`OverlayViewPolicy.IsSameOrigin:53-55` —
scheme/host/port only). **The FE's Leave control renders only when the marker is present**; with no
marker it is not offered.

**Acceptance criteria:**

- [ ] The pipe guard covers the new verb **by extension** (`OverlayPipeContractGuardTests:39-57`) — no
      second guard test.
- [ ] Launcher `hide` routes to the **same** `HideOverlayToGame()` Esc uses; no second hide logic.
- [ ] Both hosts set the marker; a marker URL still passes `IsSameOrigin`.
- [ ] No marker → no Leave control (honest absence, gap 9 option (ii)).

**Verification:**

- [ ] `dotnet test tests/FusionRpg.Guard.Tests --filter "FullyQualifiedName~OverlayPipeContract"` green
- [ ] `dotnet test tests/FusionRpg.Launcher.Tests --filter "FullyQualifiedName~OverlayPipeServer"` green
- [ ] `cd web/fusion-rpg-web; npm test -- --run overlayEmbed` green
- [ ] Live (owner terminal, both host modes): Leave hides + refocuses the game; a plain browser visit
      offers no Leave

**Dependencies:** Task 8 (the endpoint path).

**Files likely touched:**

- `src/FusionRpg.Launcher/Services/OverlayPipeServer.cs`
- `src/FusionRpg.Launcher/MainWindow.xaml.cs`
- `src/FusionRpg.Injector/Hud/OverlayViewHost.cs`
- `src/FusionRpg.Launcher/OverlayWindow.xaml.cs`
- `web/fusion-rpg-web/src/shell/OverlayLeave.tsx`, `overlayEmbed.ts` (new)

**Estimated scope:** M.

### Task 11: `docs/launcher/overlay-spec.md` — the required deliverable

**Description:** The spec's header says *"update it before changing behavior"* (`:3`). Adding a third
pipe verb, a page→host close path, and a host embed marker **is** a behaviour change to that feature.
Update the transport table (`:108`, add `hide`), the verb list (`:277`, the third verb), and the
behaviour contract (`:74-86`, the page→host close + the embed marker). The existing rule *"All three
entry points converge on one toggle method. No entry point gets its own behavior"* (`:80`) must still
hold — `hide` is the **same hide**, one more route.

**Acceptance criteria:**

- [ ] The transport table lists `hide`; the verb-list decision gains the third verb.
- [ ] The behavior contract records the page→host close and the embed marker.
- [ ] The "one toggle method; no entry point gets its own behavior" rule holds and is cited.
- [ ] This lands in the **same commit** as the code it documents.

**Verification:**

- [ ] `OverlayPipeContractGuardTests` reads the spec and stays green (`:29-36` reads the pipe name from it)
- [ ] Re-read `:74-86` and confirm the invariant still describes the code

**Dependencies:** Task 10 (the behaviour it documents).

**Files likely touched:**

- `docs/launcher/overlay-spec.md`

**Estimated scope:** S.

### Checkpoint C

- [ ] FE **Leave** hides the overlay in **both** host modes and restores game focus.
- [ ] Hide never writes the story ledger (proven at FE and Server).
- [ ] The pipe guard covers the new verb by extension; the `SendInjectorCommand` count is still two.
- [ ] `docs/launcher/overlay-spec.md` updated in-commit; the one-toggle-method invariant holds.
- [ ] No marker → no Leave.

---

## Slice 2c — `first-open-signal` (parallel with 2a/2b)

### Task 12: The durable fact + the actionability gate

**Description:** `RpgStore.Onboarding.cs` gains a durable once-per-player fact: `RecordFirstOpen(playerId)`
(`INSERT OR IGNORE`, `player_id` key, mirroring `OnboardingStoryRow` `:25` and
`EnsureOnboardingStoryRowUnlocked`'s idempotency `:44-58`) and `GetFirstOpen(playerId)`. Actionability
is the server's own `InjectorConnected` (`RpgStore.cs:1059-1060`; `LiveInjector` `:1062-1063`). Two
routes on the existing onboarding group (`OnboardingEndpoints.MapOnboarding`): `POST`/`GET
/api/onboarding/{playerId}/first-open`, with a stable reason vocabulary in the `onboarding.story-*`
style. **No capture mechanism** (decision 1).

**Acceptance criteria:**

- [ ] Recording is idempotent: twice → one row, the second is a no-op; proven **in memory**.
- [ ] Actionability flips `false` → `true` on a fresh injector heartbeat (`Heartbeat(SourceInjector)`).
- [ ] A pending fact stays pending until an injector connects; the FE is never blocked.
- [ ] Capture is **not** built; no capture claim is made.
- [ ] Capture pacing, when it lands, is a **structural per-frame cap with a comment** — never a tuning key.
- [ ] Substrate: in-memory store test; no disk, no swallowed temp-delete.

**Verification:**

- [ ] `dotnet test tests/FusionRpg.Data.Tests --filter "FullyQualifiedName~Onboarding"` green
- [ ] `dotnet test tests/FusionRpg.Server.Tests` green
- [ ] `.\scripts\verify-change.ps1 -Paths src/FusionRpg.Data/Sqlite/RpgStore.Onboarding.cs,src/FusionRpg.Server/OnboardingEndpoints.cs -Session <id>`
- [ ] Live (owner terminal): first load records once; reload does not duplicate; game closed →
      unactionable, injector connected → actionable

**Dependencies:** None (independent).

**Files likely touched:**

- `src/FusionRpg.Data/Sqlite/RpgStore.Onboarding.cs`
- `src/FusionRpg.Server/OnboardingEndpoints.cs`
- `src/FusionRpg.Injector/Host/InjectorLoop.cs` (the consume arm at the existing cadence — no new poll)
- `web/fusion-rpg-web/src/lib/bus/firstOpen.ts` (new), `src/app/App.tsx` (mount)

**Estimated scope:** M.

### Checkpoint D

- [ ] The fact records once, durably; a second load is a no-op.
- [ ] Actionability flips on injector connect; a pending fact stays pending.
- [ ] The FE is never blocked; no capture is claimed; pacing is a commented structural cap.

---

## Slice 3 — `entry-landing` (the join)

### Task 13: Land at `/sanctum` on first open — once per player, never creating a player

**Description:** `web/fusion-rpg-web/src/shell/entryLanding.ts` — a pure
`shouldLandOnSanctum({ firstOpen, embedded, pathname })` and a one-shot `useEntryLanding()` effect
mounted at the root (`App.tsx:12,21-24`). Land on `/sanctum` (where `TitleScreen`'s Continue goes,
`:34`, and where the eight rail layers unlock by play, `railState.ts:65-74`). **Never create a player**
(`SeedPlayerIfEmpty` already does, `RpgStore.cs:3865-3878`). The host keeps navigating the bare origin
(`PlaySession.cs:38`). No new route/stage/band. The story scene **already** mounts on `/sanctum`
(`SanctumStage.tsx:85-89,260-265`) — do not duplicate its trigger. Consume `commander-surface`'s seam;
edit neither its spec nor its tests.

**Acceptance criteria:**

- [ ] First open lands on `/sanctum`; a returning player keeps their route (once-per-player via the fact).
- [ ] A plain browser visit to the root still shows `TitleScreen`; `title-continue` still goes `/sanctum`.
- [ ] **No player row is created** by the landing path; no `CreatePlayer` call.
- [ ] No redirect loop: a second render does not re-navigate.
- [ ] Never redirect on an unknown fact (wait for the read).
- [ ] `commander-surface`'s spec and tests are **not** edited.
- [ ] No new route/stage/band; `npm run build` + `npm run check:bundle` green.
- [ ] No population count asserted; `/sanctum`'s rail is derived, not pinned.

**Verification:**

- [ ] `cd web/fusion-rpg-web; npm test -- --run entryLanding` green
- [ ] `npm test -- --run TitleScreen` green (unchanged)
- [ ] `npm run build` · `npm run check:bundle` green
- [ ] Live (owner terminal): first open lands on `/sanctum` (scene plays if eligible); a second open
      does not re-force it; a browser visit shows `TitleScreen`

**Dependencies:** Task 12 (the fact), `overlay-hide` Task 10 (the marker), `story-scene` +
`commander-surface` (sibling seams available — consume only).

**Files likely touched:**

- `web/fusion-rpg-web/src/shell/entryLanding.ts` (new)
- `web/fusion-rpg-web/src/shell/useEntryLanding.ts` (new)
- `web/fusion-rpg-web/src/app/App.tsx` (mount the one-shot effect)

**Estimated scope:** S.

### Checkpoint E

- [ ] First open lands on `/sanctum`; a returning player keeps their route; no redirect loop.
- [ ] No player row created; host navigates the bare origin; browser visit unchanged.
- [ ] Build + bundle budget green; sibling specs/tests untouched.

---

## Program checkpoint (after Checkpoint E)

- [ ] `overlay-hide`'s **cross-boundary** change has had its finishing full local run (Contracts + Server
      + Injector + Launcher + Web) — the one case AGENTS.md reserves it for.
- [ ] All five specs' success criteria ticked, or the unticked box is named with its reason.
- [ ] No acceptance criterion anywhere asserts a derived population count.
- [ ] ActorHub gate N/A stated in all five specs.
- [ ] **No rift-gate source calls a `UIMgr` navigation method** (Decision 17's hard rule, guard-asserted).
- [ ] **The menu has one rendering system** (uGUI art + affordance); the in-match RPG button stays IMGUI
      (Decision 18's deliberate split).
- [ ] `Sprite.Create` is the only new uGUI plumbing introduced; `ControlClick`'s click path is cited, not
      re-derived.
- [ ] Live proofs recorded per `live-probe-standard.md` §3 (state read back through the normal path).
