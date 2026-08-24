# Visual completeness audit — built FE vs. the plates

**Date:** 2026-08-24. **Method:** live screenshot of each in-scope plate section (`docs/design/*.html`,
served over HTTP so the browser would render them — `file://` is sandboxed) compared against the
actual built component source (read directly, not recalled) and, where already live-verified earlier
in the game-gui build, against real screenshots from that work. **Scope, per owner instruction:** World
stage, Battle stage, Loadout, the pact offer, and the first-session script are excluded — those are
already-excluded, not-yet-built surfaces (T16/T18/T21/T23/T24) and are not re-litigated here.

**What this is not.** Not a re-audit of the shell mechanics (bands, stage persistence, Esc/focus,
toasts, keymap, code-splitting, contract sealing) — those have their own real, passing, CI-gated
checks (Checkpoint G) and are not in question. This is specifically: **does each surface's own visual
design match the plate that specifies it.** That question was never asked as its own pass before now —
individual tasks checked function, states, and (for T8/T9/T12/T14/T17/T20/T22) their own narrow visual
slice live, but no one had opened the plate next to the built page end to end.

**Verdict, in one line.** The shell is real and matches its own rules. Several surfaces' *content*
does not match their plate — some because a real, already-documented scoping decision correctly
diverged (Relics' numeric deltas, Almanac's matchup book), and some because the surface was never
redesigned at all, only wrapped in the new shell around unmodified pre-refactor content. The second
class is new information — this audit is what surfaced it.

---

## Finding 1 — Sanctum: the rail is the wrong orientation, and most of §C's content doesn't exist

**Plate:** [01-shell-home.html](01-shell-home.html) §C. **Built:** `src/stages/sanctum/SanctumStage.tsx`,
`src/shell/Rail.tsx`, `src/stages/sanctum/FocusCard.tsx`.

Plate 01 §C draws the Sanctum as: a **vertical, left-side icon dock** (Sanctum/Creatures/Relics/
Fusion/Pacts/Expeditions/Almanac/Chronicle, one icon-over-label button per row) sharing the frame
with a two-column body — left column holds a tribute/alert banner when something is pending, a
**"Your creatures" card grid** (multiple bound creatures at once), and **"The map table"** summary
card; right column holds **"Tonight"** (expedition returns, fusable pairs) and a **"Start a run"**
CTA.

Read directly from source, the built Sanctum is:

- `Rail.tsx`: `className="... flex items-center gap-1 overflow-x-auto border-b ..."` — a **horizontal
  strip below the HUD**, not a vertical left dock. Confirmed by class name, not inferred.
- `SanctumStage.tsx`'s body (`data-testid="sanctum-body"`) renders exactly one child:
  `<FocusCard .../>`. Nothing else — no creature grid, no map-table card, no "Tonight" panel, no
  "Start a run" button exist anywhere in the stage.
- `FocusCard.tsx` has exactly two branches: the first-run script (zero creatures — this one does
  match plate §D reasonably), or `"{N} creatures bound"` + a single `ActorCard` for **only the first
  bound creature**. The plate's own stated priority rule — *"overdue tribute beats a returned
  expedition beats a fusable pair beats 'start a run'"* — has no implementation; there is no signal
  path from contract/expedition/fusion state into what the focus card shows.

This is not a partial-content gap, it is close to the entire §C composition. What ships today is HUD
+ rail + one card. What plate 01 §C specifies is a composed home screen with five real widgets. T9's
own acceptance criteria named "focus card, creature strip, map table, tonight list, run prompt" as
its own scope — only the focus card (and only its simplest branch) was actually built.

---

## Finding 2 — Creatures: no search/filter/sort, and the roster renders as rows, not the plate's card grid

**Plate:** [02-collection.html](02-collection.html) §A, §D. **Built:**
`src/layers/creatures/CreaturesLayer.tsx` (read in full).

Plate 02 §A's Creatures panel has, above the list: a search box, side filter chips (All/Plant/
Zombie), an element-colour filter, and a sort dropdown. Each list item is a full **card** — portrait,
side/level/status pills, element dots, a resource meter with numeric value, a skill-icon row, and a
POWER number.

The built `CreaturesLayer.tsx` has none of that control row — no search input, no filter chips, no
sort control exist in the component at all (confirmed by reading the full file, not a partial scan).
Its list renders `<ActorRow>` per creature — the **row** ladder rung (one line, 3–5 columns), not the
**card** rung the plate draws. Only the one *selected* creature gets an `ActorCard` treatment, in a
separate "Selected" section below the list.

This compounds with plate §D (**GG-50/GG-51**, "volume — the same layer at three magnitudes"), which
specifies a three-tier model: ≤24 renders all, 25–240 renders windowed with search present-but-
optional, **>240 goes search-first — the grid starts empty and a search/filter is required to
populate it**, and filter state must survive the layer closing. The built layer has a single
`VIRTUALIZE_ABOVE = 50` threshold (render-all vs. windowed) and, because there is no search/filter UI
at all, cannot implement the search-first tier or the filter-persistence rule — there is nothing to
persist.

---

## Finding 3 — Lawn stage: the plate's clean player HUD was never built; the shipped surface is still the pre-refactor debug harness

**Plate:** [04-run-stages.html](04-run-stages.html) §A. **Built:** `src/features/lawn/LawnPage.tsx`
(per this session's own T2/T13/T22 notes, quoted verbatim below — not re-read in full this pass, but
the claim is theirs, not inferred).

Plate 04 §A specifies a minimal, "no ornament" HUD for the stage the player spends actual combat time
on: sun count, wave/timer, a **deployed-creatures readout as chips**, and pause/1×/2× playback
controls, with the vertical rail dock still reachable.

T2 and T13's own acceptance criteria were explicitly about proving GG-11 (Phaser survives a panel
open/close) and lazy-loading the Phaser chunk — never about rebuilding the Lawn HUD. T22's own note
states plainly: *"LawnPage.tsx — purely additive, the existing debug Spawn panel and every other
existing action (spawn-extra intent, debug cell spawn, combat/shader probes) is byte-for-byte
unchanged."* That is the project's own record that the pre-refactor developer/debug control surface
is still what ships — the plate's clean sun/wave/timer/deployed/playback HUD was never built as its
own task and does not exist. This is the largest single gap by player-facing surface area: it's the
screen occupied during the actual gameplay loop.

---

## Finding 4 — Settings: three of five plate tabs, and the connection-status row, don't exist

**Plate:** [06-system-dev.html](06-system-dev.html) §C. **Built:** `src/layers/system/SystemLayer.tsx`
(grepped directly for the missing strings — zero matches for any of them).

Plate 06 §C's Settings dialog has five tabs (Game, Display, Sound, Controls, Advanced), plus a
Connection status row (health badge + Details) and a "Quit to title" action alongside "Done".

`SystemLayer.tsx` has zero matches for `Display`, `Sound`, `Advanced`, `Connection`, or `Quit to
title` — confirmed by grep against the real file, not by recollection. Only **Game** (4 toggles) and
**Controls** (rebinding) exist, matching T20's own stated scope exactly — T20 never claimed the other
three tabs or the connection row, so this is an honest scope boundary, but the plate promises it and
no task closes the gap. (Audio being disabled with a stated reason is a separate, already-accounted-
for decision — `game-gui-map.md` assumption 7 — the missing piece is the tab and the Display tab
existing at all, not the Sound toggles inside it.)

Plate §D's Controls "verb table" (a read-only reference grid of *every* global verb, including
non-rebindable ones — Esc/Tab/Space/`?`/F10/backtick) is also not fully mirrored: the built Controls
tab shows one row per **rebindable** action only (matching T20's own stated scope), not the plate's
complete reference grid of all verbs. The rebinding flow itself (Change → listening → conflict named
inline with Keep/Take it) does closely match the plate, including the exact "Keep {key}" wording T20's
own live pass already verified.

---

## Finding 5 — The "thin-wrap" layers kept their pre-refactor visual content, not the plates'

**Plates:** [02-collection.html](02-collection.html) §C (Fusion — inapplicable, see note),
[03-world.html](03-world.html) §C (Expeditions), [05-chronicle-almanac.html](05-chronicle-almanac.html)
§A/§C (Almanac/Chronicle). **Built:** `ExpeditionsLayer.tsx`, `AlmanacLayer.tsx`, `ChronicleLayer.tsx`
— all confirmed, per their own task notes already on record, to be thin `PanelShell` wraps around
unmodified pre-refactor pages (`ExpeditionsPage.tsx`, `CatalogPage.tsx`/`RecipesPage.tsx`,
`MetricsPage.tsx`/`RpgProgressionPage.tsx`/`PvzStatsPage.tsx`).

This is the pattern that explains most of the individual gaps above and below it, so it is called out
once, structurally, rather than four times:

- **Fusion** — plate 02 §C draws *creature* fusion (two creatures in, one out). The built Fusion is
  the real, already-shipped **demon** fusion lab — a different domain entirely, per T15's own owner
  decision. The plate section is simply not applicable to what ships; not a gap, a resolved mismatch.
- **Expeditions** (plate 03 §C) — the plate's rich card list (icon, status pill, results row with
  item/XP/souls chips, progress bar, roster chips, an "Empty berth · Dispatch" card) is not what
  renders; `ExpeditionsLayer` wraps the pre-existing `ExpeditionsPage.tsx` verbatim, whose own visual
  design predates this refactor and was never checked against plate 03.
- **Almanac** (plate 05 §A) — the plate's master-detail creature browser (searchable list with
  silhouetted undiscovered entries, a detail pane with portrait/flavour-text/stat grid/element
  matchups/fuses-into chips) does not exist. This is a **known, already-reasoned** gap — T19's own
  note says the richer content "has no real backing" — confirmed here visually rather than newly
  found: the live Almanac shows a flat "Type catalog" table only, matching the honest-scope claim.
- **Chronicle** — not independently re-checked this pass beyond the T19 live-verification already on
  record (real seeded data, `DivergingBar` confirmed rendering); no new finding.

**Pacts** is the one exception worth naming precisely, because it is not a thin wrap — T17 built it as
a real, dedicated component (`PactsLayer.tsx`, read in full). It is closer to its plate (03 §D) than
any of the above: real loyalty bars, real conditional actions with inline disabled-reasons, real
tribute-overdue styling. Two structural differences remain, confirmed by source: it renders pacts as a
**vertical stack** (`flex flex-col gap-3`) rather than the plate's **side-by-side card pair**, and it
has no portrait/icon per card at all (the plate's cards each show a colour-tinted creature portrait).
Moderate, not major — the information content is real and present, only the layout density differs.

---

## What this audit deliberately did not re-check

- **Relics** (plate 02 §B) — the missing numeric stat deltas and the stacked-not-beside comparison
  layout are both real, but both are already-documented, owner-reasoned scope decisions from T14 (no
  stat plugin computes a relic's magnitude yet; `PanelShell`'s 640px cap makes the plate's two-column
  assumption unworkable). Reconfirmed visually, not a new finding.
- **Foundation / entity ladder** (plate 00) — T8 already live-verified the actor ladder against plate
  00 §D.2 at three widths with real screenshots. Not re-run.
- **Deploy targeting** (plate 07 §B) — closely matches; T22's own live pass already confirmed the
  banner/lit-cells/Esc-cancel flow. The plate's "COSTS 150 SUN" badge has no real analogue since
  Deploy places an already-owned bound creature rather than spending sun on a fresh spawn — a domain
  difference, not an implementation gap, in the same family as T14/T15/T17's earlier domain-mismatch
  findings.

---

## Summary table

| # | Surface | Plate | Severity | Class |
|---|---|---|---|---|
| 1 | Sanctum home stage | 01 §C | **Major** | Never built beyond HUD+rail+one card |
| 2 | Creatures search/filter/volume tiers | 02 §A, §D | **Major** | Never built |
| 3 | Lawn player HUD | 04 §A | **Major** | Never built — still the pre-refactor debug surface |
| 4 | Settings tabs (Display/Sound/Advanced) + connection row | 06 §C | **Moderate** | Never built |
| 5a | Expeditions content | 03 §C | **Moderate** | Thin-wrap, pre-refactor visuals kept |
| 5b | Almanac content | 05 §A | Known/reasoned | Thin-wrap, already documented (T19) |
| 5c | Pacts layout density (stack vs. card pair, no portraits) | 03 §D | Minor | Real component, layout differs |
| 5d | Rail orientation (horizontal strip vs. vertical dock) | 01, 02, 04 | **Major**, cross-cutting | Affects every stage/layer simultaneously |
| — | Relics numeric deltas / layout | 02 §B | Known/reasoned | Already documented (T14) |
| — | Fusion domain | 02 §C | Resolved, N/A | Owner redirected to demon fusion (T15) |
| — | Deploy targeting | 07 §B | Matches | Already live-verified (T22) |

**Rail orientation (5d) is called out separately** because it is not one surface's gap — `Rail.tsx` is
shared by every stage, so fixing its orientation is a single, cross-cutting change with the highest
leverage of anything on this list, and it should be sequenced before any of the content-shaped fixes
above so those don't have to be redone around it.

---

*Plate 01 · Sanctum §C, plate 02 · Creatures §A/§D, plate 04 · Lawn §A and plate 06 · Settings §C were
screenshotted live against the served plates for this audit; screenshots are session-local and not
committed. Re-run against `docs/design/*.html` (served over HTTP, not `file://`) to reproduce.*
