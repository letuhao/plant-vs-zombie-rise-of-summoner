# Spec: world-lenses

**Status: Draft — Phase 1 (Specify), awaiting owner review.** Module id `world-lenses` in the
[world-stage capability map](../world-stage-map.md). **Level 4**, depends on `world-render`.

**Ideal:** [world-stage-ideal.md](../world-stage-ideal.md) §4.8, §4.9, §8c.1, §8c.5.
**Catalog:** [design/11-world-stage.html](../../design/11-world-stage.html) §F (F.1 picker, F.2
auto-activation, F.3 the rejected alternative).

---

## Objective

Give the map **six exclusive information layers**, a picker that names the one that is on, number-row
hotkeys, and auto-activation from selection — so the lens the player would have had to remember is
the one that turns itself on.

Today there are none. There is one boolean — `lifelines` — held in a `useState` in the page
(`WorldPage.tsx:63`), toggled by a text button labelled `"Lifelines"` / `"Hide lifelines"`
(`:206-207`), and applied by **swapping the node component type** for the whole graph
(`:226`: `nodeTypes={lifelines ? lifelineNodeTypes : nodeTypes}`). That is the shape a lens system
has to replace: one lens, expressed as a component swap, with no name on screen, no key, and no way
to ask for any of the other five facts the map already holds.

**Success is that exactly one lens is on at all times, its name is on screen, a number key selects it,
and no lens is legible only by hue.**

## Design

### 1. The six, and why the set is closed at six

Exclusive — a radio group, never checkboxes. Two layers of meaning over one map is how a player stops
being able to tell what a colour means.

| Key | Lens | What every sector answers | Encoding that is not colour | Server cost |
|---|---|---|---|---|
| `1` | **Ownership** | yours · theirs · free · unseen | Solid double edge · hatch · dash · dot, each captioned | free — already on the state read |
| `2` | **Loam flow** | earns minus costs, this turn | `▲` / `▼` plus a **signed number**; ground that is not yours shows `—`, never `0` | free — `WorldDtos` carries income/upkeep/net per sector |
| `3` | **Fade risk** | firm · slipping · lost next turn | The **word** "lost next turn", plus a warning glyph | free — `WillReleaseNextTurn` is on the wire |
| `4` | **Supply & lifelines** | which roads, if cut, halve your territory | Thick rule on the road + a caption on the hinge sector | **paid** — see §4 |
| `5` | **Intel age** | never seen · rumoured · scouted · watched | Parchment hatch + **the age in turns**, and a distinct silhouette for never-seen | free — `intel` / `intelAge` are on the wire |
| `6` | **Danger** | the band | **Counted diamonds** (`◆◆◆`), because the band *is* a count | free — `dangerBand` is an int |

Six is a closed set and adding a seventh is a decision, not a convenience: every lens is a thing the
player has to learn **and** a thing every sector has to answer. A lens that cannot answer for every
sector is a highlight, not a lens.

**Ownership is the home lens.** Pressing the active lens's own key returns to Ownership, so there is
always one key that means *"show me the map again"* without the player having to remember which key
that is.

### 2. Placement is a transient targeting overlay, and it is not a lens

This is the rule that keeps the set of six honest, and an earlier draft of plate 11 broke it — the
"closed set of six" had a seventh member in it (repaired 2026-09-03, defect class 10).

| | A lens | The placement overlay |
|---|---|---|
| Lifetime | The whole session, until another is chosen | Alive only while the verb is |
| Picker slot | Yes, one of six | **None** |
| Hotkey | A number key | **None** |
| Exclusivity | Replaces the previous lens | Draws **over** the current lens and restores it on exit |
| Owner | This module | `world-targeting` |

Placement, and every range overlay in plate 11 §E.3, is `world-targeting`'s. This module's only
obligation to it is the restore contract in §3: whatever lens was showing when a targeting overlay
opened is the lens showing when it closes.

### 3. Auto-activation, and the two promises that make it safe

The best property of a picker-based system is that the picker does not have to be used. Choosing to
raise a structure turns the placement overlay on without the player remembering that one exists.

| Trigger | What turns on | Why unasked is safe |
|---|---|---|
| Choose **Raise** | The placement overlay (not a lens — `world-targeting`'s) | The verb has no meaning without it, and nothing the player could want to see is hidden |
| Choose **Ward a road** | Lens `4` Supply & lifelines | A ward's target is an edge, and lens 4 is the only one that draws roads as first-class. Distinct from binding a warden to a *sector* — `WardLevel` sits on a lane, `WardenBindingId` on a sector, and the engine separates them |
| Select a legion **outside supply** | Lens `4` Supply & lifelines | The one question that legion raises is *"can it get home"* |
| Open a **fade warning** from the notification rail | Lens `3` Fade risk, centred on the sector | The notification and the lens are about the same fact; splitting them is Amplitude's "Divided UI" mistake in miniature |

**Two promises, both testable:**

1. **It announces itself.** The picker's active state changes visibly and the readout says the new
   lens's name. An information layer that swapped itself silently is indistinguishable from a
   rendering bug.
2. **It is undoable in one gesture, and it restores rather than resets.** Esc, or completing the
   action, puts back **the lens the player chose** — not Ownership, unless Ownership is what they
   chose. The module holds a single `playerChosenLens` alongside the effective one; auto-activation
   writes only the effective one.

### 4. Lens 4 costs a network round-trip, and that is what makes it a lens

`?lifelines=true` is an opt-in server cost gate, and the comment above it says why in the server's own
words: *"Reconnection cost is an O(holdings⁴) sweep and the overlay it feeds is off by default, so it
is asked for rather than always paid for"* (`WorldEndpoints.cs:48-51`).

The client already threads it: `useWorldState(worldId, { lifelines })` puts the flag **in the query
key** (`lib/bus/world.ts:80`) and appends `lifelines=true` to the URL (`:86`). So selecting lens 4 is
a different cache entry and a fetch, not a re-render.

Three consequences the module owns:

- **Lens 4 has a loading state and the other five do not.** GG-17 makes loading a designed state, so
  the picker's lens-4 chip carries a pending treatment and the map keeps drawing the previous lens
  underneath until the lifeline data arrives. It must never blank.
- **Leaving lens 4 does not discard the result.** The query key difference means React Query holds
  both; returning to lens 4 within `staleTime` is instant.
- **This is the pattern for any future lens with a server cost**, and it is the second reason the set
  is closed at six: a lens that costs a sweep has to be asked for, and a set of fifteen would be asked
  for by accident.

### 5. The picker lives in the bottom-left cluster, and the cluster's role never changes

One cluster, bottom-left, holding zoom, fit and the lens picker —
`information-architecture.md` §2.2's *"map controls cluster (zoom / fit / layers / fog)"*. Band 1,
anchored, and per §8d.3 **not scrimmed** when a band-2 inspector opens.

The readout **always names the active lens in words** (`1 / 6 · Ownership`). A player who walked away
mid-turn must be able to tell what they are looking at without touching anything — which is precisely
the property ES2's zoom-coupled Scan view cannot have, and the reason §4.8 rejected it: when a layer's
identity is only its zoom depth, two layers converging is an **invisible** bug. It happened to ES2's
Economy and Trade scans after an economy rework, and players could not tell it was a bug because there
was no name on screen to disagree with.

### 6. The hotkey trap — `registerGlobalVerb` **throws**, and rebinding can reach the number row

This is the one place this module can crash the stage, so it is specified rather than assumed.

- `registerGlobalVerb(key, id, handler)` throws on a duplicate: *"`"${key}"` is already registered by
  `"${existing.id}"` — every global verb has exactly one owner"* (`shell/keymap.ts:45-51`). It is a
  programming error by design, not a runtime condition to swallow.
- The eight rail verbs default to **letters** — `c k r f p e a h` (`layers/system/keybindings.ts:22-31`)
  — so the number row is free *by default*.
- **But the bindings are player-rebindable, and `rebind` does not know the number row is reserved.**
  `conflictFor` scans only the eight `BindableActionId`s (`keybindings.ts:86-93`) and `rebind` writes
  whatever key it is given (`:102-112`). A player who binds Relics to `3` makes this stage throw on
  mount, on a code path no test covers.

**The fix is to enforce the rule that already exists, not to defend against it.**
`information-architecture.md:172` declares `1`–`9` as *"Stage-specific hotbar · owned by the current
stage"* — so a rebind onto a digit is already a rule violation, and it should be refused at the
rebind, in `keybindings.ts`, with a reason the Controls screen can show (GG-55). A defensive
`try/catch` around registration is explicitly **not** the fix: it would hide a broken rebind behind a
silently dead hotkey.

Registration follows `SanctumStage.tsx:165-177`'s shape exactly — register in an effect, return the
unregister array from the cleanup — so leaving the stage frees `1`–`6` for the next stage's hotbar.

### 7. Colour is never the only channel, in any lens

GG-27 (squint test) and GG-30 (contrast) apply to all six, and this module is where they are most at
risk, because a lens is by nature a re-colouring. The rule from §4.9 holds for lenses as it does for
fog: **shape and pattern and a word carry the fact; the tint only agrees with it.**

The evidence is blunt and it is in the ideal: the most-subscribed mods for both Endless games are
palette expansions (≈22,600 and ≈21,800 subscribers), and a 2,697-subscriber ES2 mod exists solely
because *"the color of the label indicating a planet is colonizable is exactly the same as the color
indicating it is not colonizable."* Colour-coded density fails at scale and players pay money to fix
it.

Concretely, per lens: ownership is four **patterns**; loam flow is an **arrow plus a signed number**;
fade risk is a **word**; supply is **line weight plus a caption**; intel age is a **hatch plus a
number of turns**; danger is a **count of diamonds**. Every one survives a greyscale print, and a test
asserts the text channel exists for each.

## What stays out

- **The range and placement overlays.** `world-targeting` owns them; §2 is the boundary.
- **The fog treatments themselves.** `world-render` owns the four intel states as a rendering
  concern; lens `5` *foregrounds* them, it does not define them.
- **The camera, zoom and fit controls.** `world-shell` owns those; this module contributes the picker
  that sits beside them in the same cluster.
- **The magnitude rendering inside a lens badge.** `world-numbers` owns it — a `+34` in the loam lens
  goes through the same unit-family-declaring renderer as every other number on the stage.
- **A fog toggle.** `information-architecture.md` §2.2 lists one in the cluster; whether the player may
  turn fog *off* is a game-design question this program does not decide, and it is not one of the six.

## Commands

```powershell
cd web\fusion-rpg-web
npm test                 # vitest run
npm run build            # tsc --noEmit && vite build
npm run lint
```

The keymap guard runs inside the test suite (`shell/keymapGuard.test.ts`), so a global verb bound
outside `keymap.ts` fails `npm test`.

## Project structure

```
web/fusion-rpg-web/src/
  stages/world/
    lenses/
      lensCatalog.ts        → the six, closed: id, key, label, and the encoding contract
      lensState.ts          → pure: active lens, playerChosenLens, auto-activate, restore
      lensState.test.ts
      LensPicker.tsx        → the bottom-left chip row + readout
      LensPicker.test.tsx
      useLensHotkeys.ts     → registerGlobalVerb 1..6, unregistered on unmount
  layers/system/
    keybindings.ts          → refuse a rebind onto 1-9 (IA §5's reserved range), with a reason
```

## Code style

The catalog is data, the state is a pure reducer, and the component reads both. Nothing about a lens
is decided inside a renderer.

```ts
/** The closed set. Adding a seventh is a spec decision — see §1. */
export const LENSES = [
  { id: "ownership", key: "1", label: "Ownership", cost: "free" },
  { id: "loam",      key: "2", label: "Loam flow", cost: "free" },
  { id: "fade",      key: "3", label: "Fade risk", cost: "free" },
  { id: "supply",    key: "4", label: "Supply & lifelines", cost: "server" },
  { id: "intel",     key: "5", label: "Intel age", cost: "free" },
  { id: "danger",    key: "6", label: "Danger", cost: "free" }
] as const;

export type LensState = {
  /** What is drawn right now. */
  active: LensId;
  /** What the player last chose. Auto-activation never writes this — see §3. */
  playerChosen: LensId;
};
```

## Testing strategy

Vitest, colocated. Five levels, and the last two are the ones that would otherwise ship broken:

1. **Exclusivity** — every reducer path leaves exactly one lens active. There is no state in which
   zero or two are on, and the type does not permit one.
2. **The home key** — pressing the active lens's own key returns to Ownership; pressing `1` while on
   Ownership is a no-op, not a toggle to nothing.
3. **Auto-activation restores, it does not reset** — choose lens `6`, select an out-of-supply legion,
   assert `active === "supply"` and `playerChosen === "danger"`, then Esc and assert `active` is back
   to `danger`. This is the test that catches the obvious wrong implementation (restore to Ownership).
4. **Hotkey lifecycle** — mounting the stage registers `1`–`6`; unmounting frees them; **mounting
   twice in a session does not throw.** Plus the rebind guard: `rebind("relics", "3")` is refused with
   a reason, and a test asserts the stage still mounts afterwards.
5. **Colour independence, per lens** — for each of the six, the rendered node exposes a **text or
   pattern** channel carrying the fact, asserted by role/text query rather than by class name. Six
   tests, one per lens, because a regression will land in exactly one of them.

Lens 4's fetch is asserted at the hook level: selecting it changes the query key
(`lib/bus/world.ts:80`), and the map keeps rendering the previous lens while the request is in flight
rather than blanking.

## Boundaries

- **Always:** keep exactly one lens active; name it on screen in words; carry every lens's fact in a
  non-colour channel; register hotkeys through `keymap.ts` and unregister on unmount; restore the
  player's chosen lens after an auto-activation.
- **Ask first:** a **seventh** lens — the set is closed by design and §1 says why. Any change to
  `registerGlobalVerb`'s throw-on-duplicate semantics, which four other surfaces depend on. Any lens
  that would need a new server projection.
- **Never:** stack two lenses. Never let a lens be the sole meaning of a hue or an opacity. Never bind
  a key outside `keymap.ts` (`keymapGuard.ts` fails the build for it). Never swallow a
  `registerGlobalVerb` throw — fix the rebind that caused it.

## Success criteria

1. Six lenses exist, are exclusive, and the active one's **name** is on screen at all times.
2. `1`–`6` select directly; the active lens's own key returns to Ownership; the keys are freed when
   the stage unmounts.
3. A rebind onto `1`–`9` is refused with a player-readable reason, and a test proves the stage mounts
   afterwards — the crash path in §6 is closed at its source.
4. Lens 4 is the `?lifelines=true` read, with a designed loading state, and switching away and back
   does not refetch within `staleTime`.
5. Four auto-activation triggers work, announce themselves, and **restore the player's chosen lens**
   on Esc or completion.
6. Placement has no picker slot and no hotkey — a test asserts the catalog has exactly six entries.
7. Each of the six carries its fact in a non-colour channel, proven by six per-lens tests.
8. `npm test`, `npm run build` and `npm run lint` are green.

## Open questions

**None.** §4.8 decided the picker over zoom-coupling, §8b listed the six, and plate 11's defect-10
repair settled placement's status. The hotkey collision in §6 is a defect with a named fix, not a
question.
