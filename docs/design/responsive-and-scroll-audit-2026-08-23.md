# Responsive and scroll audit — the awareness the nine documents assumed but never stated

**Status:** Audit + fix, 2026-08-23. Prompted directly by the owner: *"first is responsive awareness,
second is add scrollable for large model, like actor stats view, item options view."* Both turned out
to be real, load-bearing gaps — not caution. One was a live defect already sitting in the finished
plate, found by measurement, not by eye.

**Verdict: two rules were missing from [game-gui-principles.md](../architecture/game-gui-principles.md),
one component had zero bound where it needed one, one component's fix was silently overridden by a
leftover declaration in the same rule, and one plate section wasn't wired to the shared component
contract at all.** All four are fixed. Recorded here so the pattern — audit the *mechanism*, not the
screenshot — is on record the way [gap-audit-2026-08-22.md](gap-audit-2026-08-22.md) already is for
the entity coverage.

---

## 1. Finding 1 — GG-36 named a range and never declared it

[game-gui-principles.md §7](../architecture/game-gui-principles.md), GG-36: *"Surfaces are built
against a **declared range** of viewports and aspect ratios."* Grepped across the whole design set —
`tech-stack.md`, `game-gui-map.md`, `gap-audit-2026-08-22.md` — every one of them cites *"the declared
viewport set"* or *"the declared widths."* **None of them ever states a number.** The rule pointed at
itself in a circle.

**Fixed by grounding it in the one place the codebase already reasons about this**, rather than
inventing a figure:
[`OverlaySwitchLayout.cs`](../../src/FusionRpg.Core/Overlay/OverlaySwitchLayout.cs) sizes the in-game
overlay toggle button and states its own floor in a comment — *"never shrink: the button is already
small, and a 720p target would be unhittable"* — pins `ReferenceHeight = 1080f`, and caps `MaxScale`
at `3f`. That is real evidence about the range of displays this game is expected to run on, verified
by reading the file, not assumed.

**Declared, corrected into GG-36 directly:**

| Bound | Value | Source |
|---|---|---|
| Height floor | 720 CSS px | the injector's own stated 720p-unhittable floor |
| Height reference | 1080 CSS px | `OverlaySwitchLayout.ReferenceHeight` |
| Height ceiling | none | `MaxScale = 3` — the injector already treats headroom as generous |
| Width floor | 1280 CSS px | the height floor at 16:9, the narrowest common desktop ratio |
| Width reference | 1440 CSS px, centred | no wider column existed to cite; declared so GG-37's "centred and bounded" has a number |
| Width ceiling | none | ultrawide gets more room, never less |

**CSS pixels, stated as a reason, not an afterthought.** WebView2 is Chromium and inherits standard OS
DPI scaling — a 4K panel at Windows' common 200% default already presents roughly the same CSS-pixel
viewport as 1080p at 100%. The declared numbers are CSS pixels, which is what layout actually measures
against.

**A margin already banked, named as such.** This session's plates were swept at 800×900 throughout —
narrower than the now-evidenced 1280px floor. That is extra headroom already cleared, not the official
contract; the real test points are **1280×720, 1440×900, 1920×1080**, all three verified in §4.

---

## 2. Finding 2 — no rule existed for one entity's own content, only for many entities

GG-50 (Tier 1) already covers *collections* — a list of 1,000 items gets virtualization or a
search-first threshold. **Nothing covered a single, bounded entity whose own detail is taller than the
viewport** — exactly the two cases the owner named: an actor's derived-stat sheet (document 9, up to
141 rendered rows), an item's full card once affixes, enhancement, sockets and a set block are all
populated (document 2, eleven blocks). Grepped for any existing "internal scroll" or "dense component"
rule — zero hits anywhere in the principles or design set.

**New rule, GG-61**, added to [§14 "Reach, extended"](../architecture/game-gui-principles.md) beside
GG-56–GG-59 (its natural neighbours — CJK overflow tolerance, pointer reach, art fallback, chrome
performance) and placed in **Tier 1** alongside GG-50, for the same reason GG-50 is Tier 1: *"this
decides component shape before it decides performance … not convertible into a windowed list without
rewriting every consumer."* A shell built without an internal-scroll body has exactly the same
retrofit cost.

> **A dense entity scrolls inside its own shell; the shell never grows to swallow the viewport.**

Full text, the distinction from GG-50, and the test are in the rule itself.

---

## 3. Finding 3 — `PanelShell` had no height bound at all

`_kit/kit.css`'s `.panel-shell` set `.body { overflow: auto }` with **nothing bounding the shell's own
height** — the declaration was inert. Grepped every use of `.panel-shell` across the plate: **one**
instance (the band-shell illustration under GG-1/GG-5/GG-11, `style="height:100%"` inside a bounded
`.viewport`) gave the shell a real height. The other three — the actor panel (document 6 territory),
the derived-stat sheet (document 9), a dialog variant — had no bound, meaning they were free to grow
the *page*, not their own container, the moment real content pushed past one screen.

**Fixed** — `.panel-shell` now declares `max-height: min(720px, 82vh)`, `header`/`footer` get
`flex: none` so they never compress, and `.body` gets `flex: 1; min-height: 0; overflow-y: auto`, the
combination that actually lets a flex child's `overflow: auto` engage. 82vh leaves HUD room at the
720px floor; the 720px absolute cap stops the shell stretching pointlessly tall on a 4K/ultrawide
headroom viewport where more room was never the problem.

---

## 4. Finding 4 — the fix for the item card was overridden by a line already in the file

`.itemcard` (document 2) had **no height bound at all**, the same defect as `.panel-shell`. Added the
identical fix — `max-height`, `overflow-y: auto` — and measured it: **`overflow-y` computed to
`hidden`.** The rule already ended with a bare `overflow: hidden` (controlling the card's rounded
corners), and because it appeared *after* the new longhand declarations in the same rule, it won the
cascade and silently cancelled them.

**This was not a hypothetical.** Measured against the plate's own fully-built item card (document 2's
eleven blocks, every one populated): `scrollHeight = 945px` against `clientHeight = 719px` — **226px of
real content, including the card's own footer, was being clipped with no scrollbar and no visible sign
anything was missing.** A screenshot taken before this fix looked correct, because the clipped region
is by definition absent from a screenshot. Caught only by asserting `scrollHeight` against
`clientHeight` in the browser, which is the entire argument for why GG-61's test is a measurement and
not a look.

**Fixed** by removing the trailing `overflow: hidden` and letting the explicit `overflow-y: auto` /
`overflow-x: hidden` pair stand. Re-measured: `overflow-y` now computes `auto`, the card scrolls, all
945px of content is reachable, the header and footer stay visible at the top and bottom of the
scrollable region.

---

## 5. Finding 5 — the stat sheet wasn't wired to the shell it claimed to be

Document 9's derived-stat sheet used `<div class="panel-shell">` for its outer wrapper but **`<div
class="hd">` / `<div class="bd">`** for its header and body — not the canonical `header` / `.body`
[`PanelShell`](_kit/kit.css) actually styles. Grepped: `.hd` with no scoping selector matched
*nothing* in the stylesheet (every real `.hd` rule is scoped, e.g. `.card .hd`), so the sheet's header
row carried none of the shell's padding or background, and — the load-bearing part — **the finding-3
fix to `.panel-shell > .body` could never reach it**, because there was no element with class `body`
inside it.

**Fixed** by converting the markup to the real contract: `<header>` wrapping the title and the "show
unchanged" toggle, `<div class="body" style="overflow-x:auto">` wrapping the table (the inline rule
keeps the table's own horizontal scroll; the class now also supplies vertical scroll via finding 3's
fix). Re-verified with a synthetic load test — see §6.

---

## 6. Verification

**Zero horizontal page scroll, zero overflowing elements**, swept at all three declared points:

| Viewport | Result |
|---|---|
| 1280×720 (floor) | Clean. Shells correctly shrink to `82vh = 590.4px` here, since it is tighter than the 720px cap at this height |
| 1440×900 (reference) | Clean |
| 1920×1080 (headroom) | Clean. 6 shells checked (`panel-shell` + `itemcard`), 0 exceed their declared bound |

**The internal-scroll mechanism proven under load, not just declared.** Two direct tests:

- **Item card (live, already-present content):** `scrollHeight 945px` vs `clientHeight 719px` before
  the fix (content silently clipped) → `overflow-y: auto`, fully scrollable, after it.
- **Derived-stat sheet (synthetic — 30 rows appended via script to simulate a fuller actor):** shell
  held exactly at its `720px` cap, body went from `scrollHeight 489` (fits, no scroll needed) to
  `scrollHeight 1147` against `clientHeight 631` (scroll engages), header stayed visible throughout.

Both prove the same thing from opposite directions: a shell that's too small for its content scrolls;
a shell with room to spare doesn't force a scrollbar it doesn't need.

---

## 7. What changed, filed by location

| File | Change |
|---|---|
| [`architecture/game-gui-principles.md`](../architecture/game-gui-principles.md) | GG-36 amended with the declared range + evidence; new **GG-61** added to §14, Tier 1 in §17, a new enforcement row in §19, a new §20.4 decision entry, rule count corrected 60→61 throughout |
| [`decisions.md`](../architecture/decisions.md), [`game-gui-map.md`](../architecture/game-gui-map.md), [`gap-audit-2026-08-22.md`](gap-audit-2026-08-22.md), [`information-architecture.md`](information-architecture.md), [`README.md`](README.md), [`web/spec.md`](../web/spec.md) | Every `GG-1…GG-60` citation corrected to `GG-1…GG-61`; `decisions.md`'s hard-gate count corrected 10→11 |
| [`_kit/kit.css`](_kit/kit.css) | `.panel-shell` gains `max-height`, `header`/`footer` gain `flex: none`, `.body` gains `flex:1; min-height:0; overflow-y:auto`. `.itemcard` gains the same bound, and its conflicting trailing `overflow: hidden` is removed |
| [`00-foundation.html`](00-foundation.html) §D.5 | The stat-sheet panel's `.hd`/`.bd` markup converted to the canonical `header`/`.body` contract |

---

## 8. The seven other plates — swept 2026-08-23, clean

The finding-3/4 fixes to `_kit/kit.css` are global, so every plate sharing the kit inherited them
immediately — the question was whether any of the other seven carried the same `.hd`/`.bd`-style drift
that made document 9's fix inert, or a shell that now exceeds its new bound.

**Twenty `panel-shell` instances across six plates** (02, 03, 04, 05, 06, 07 — 01 has none), every one
checked for markup and measured for compliance:

| Check | Result |
|---|---|
| Non-canonical header/body markup (the document-9 pattern) | **None found.** All 20 use the real `<header>` element; `header` and `class="body"` counts match the `panel-shell` count 1:1 in every file |
| A shell exceeding its declared `max-height` | **None.** 0 of 20, measured directly against `getBoundingClientRect()` vs the computed `max-height` |
| A body that should scroll but doesn't | **None.** Every body reports `overflow-y: auto`; the one case with real overflow (Creatures panel, 02-collection.html — 475px of content in a 447px client area) scrolls correctly rather than clipping |
| Page-level horizontal scroll, all eight plates, at all three declared points (1280×720 / 1440×900 / 1920×1080) | **Clean everywhere.** 24 combinations checked, plus the two content-heaviest plates (02, 06) spot-checked twice |

Document 9's drift was a one-off — built fresh in this session rather than copied from the
already-established pattern the other seven plates already followed correctly. Confirmed, not
assumed: this is the argument for measuring rather than trusting a screenshot, applied to the sweep
itself.

## 9. What this still does not cover

- **A real virtual-scrollbar or "scroll to reveal more" affordance design.** GG-61 states the
  mechanism (bounded shell, internal scroll) and proves it engages; it does not design the scrollbar's
  visual treatment, a fade-to-indicate-more-content edge, or momentum/touch scrolling — implementation
  polish, not a structural gap.
- **Band-3 dialogs and band-4 toasts** were not individually load-tested against GG-61 the way the
  actor panel and item card were — `.panel-shell dialog` exists on several plates and inherits the same
  bound structurally, but no dialog was pushed past its bound the way the stat sheet and item card were
  synthetically loaded in §6. Deferred deliberately: a dialog is a short confirmation by nature, not the
  dense-entity case this rule targets, so the risk is lower and the check was judged lower-priority
  rather than skipped by oversight.

---

## 9. Design-gate checklist

```
[x] I identified the subsystem(s) this touches — the shared kit's shell components, the principles'
    reach section, every cross-reference to the GG rule count.
[x] I read the existing rule before amending it — GG-36's full text, not just its title, before
    concluding it was underspecified rather than simply unread.
[x] I checked decisions.md for a lock covering this — none existed; §20.4 records the two additions.
[x] Every factual claim cites file:line — OverlaySwitchLayout.cs's constants and comment, kit.css's
    exact rule ordering that caused the override, the measured scrollHeight/clientHeight numbers.
[x] I verified claims against CODE and against the RENDERED PAGE, not against a screenshot — the
    item-card defect is specifically the case a screenshot cannot catch, which is why it was found by
    asserting scrollHeight > clientHeight in the browser instead of eyeballing the plate.
[x] I read the surrounding section of every rule I quoted (GG-50 before claiming GG-61 was distinct
    from it; GG-36's own Why before amending its evidence).
[x] I tested the constraint, not assumed it — the internal-scroll mechanism was proven under both a
    live-content case (the item card) and a synthetic-load case (30 appended rows), not just declared
    and trusted.
[x] Nothing contradicts a §2 invariant.
[x] Corrections propagated — six cross-file GG-60 citations corrected in the same pass, not left
    stale after the principles doc itself was fixed.
```
