# Spec: world-notify

**Status: Draft — Phase 1 (Specify), awaiting owner review.** Module id `world-notify` in the
[world-stage capability map](../world-stage-map.md). **Level 5**, depends on `world-hud` and
`world-turn`.

**Ideal:** [world-stage-ideal.md](../world-stage-ideal.md) §4.7, §4.6, §8c.1, §8d.3.
**Catalog:** [design/11-world-stage.html](../../design/11-world-stage.html) §H.3–H.6.

---

## Objective

Two notification classes with an honest boundary between them: **band-4 toasts for things requiring
action**, a **passive right-edge rail** for everything else, **flushed on End Turn except blockers**,
with the category→channel control living **on the notification**.

**The rule is not "fewer notifications". It is that weight must track importance.** Endless Space 2
states both failure directions in the same breath — it *"bombard[s] you with notifications for minor
things like population growth"* while it *"does not notify you when one of your systems is under
siege"* — and Civ VII's indictment is the same sentence from a different game: an interface *"is
incapable of differentiating between what's a bother and what's important."* An interface can be
correct on every line and still fail this.

**Half of it already ships.** `shell/toastStack.ts` is a working band-4 stack — a zustand store with
auto-expiry, timer cleanup and a `clear()` (`toastStack.ts:29-51`) — rendered by `Toasts.tsx`, which
is `pointer-events-none` at the container so it never blocks input (`Toasts.tsx:11-19`), and fed
globally by the `MutationCache` listener at `lib/bus/mutationFeedback.ts:17-32`. **Only the
passive rail, the categories and the settings model are new**, plus two additions to the toast.

**Success is that a routine event costs zero clicks and an important one costs one.**

## Design

### 1. Two classes, and the boundary is the player's next action

| | Band 4 · toast | Band 1 · rail |
|---|---|---|
| Carries | Things **requiring action** | Everything else |
| Lifetime | Auto-expires (5s default, `toastStack.ts:22`) | Until dismissed, or until the End Turn flush |
| Interaction | An **action button** — "Show me" | Open, act, dismiss, or ignore |
| Blocks input | Never (`pointer-events-none` container) | Never |
| Cap | **At most three at once**, newest on top, remainder behind a count on the rail | The rail scrolls (GG-61) |

The three-at-once cap is transferred from general UI practice, not from games, and it is written down
as guidance rather than as evidence — which is the honest way to carry a borrowing whose source is
weaker than the rest.

**Band placement is settled and non-obvious.** The rail is **band 1**, not band 2: it is HUD, it is
anchored, and per §8d.3 a band-2 inspector's scrim does not cover band 1. That decision exists
because the shipped kit gets it wrong today — `.scrim` is `z-index: var(--band-panel)` (`kit.css:401`) = **200** against
`--band-hud` = **100** (`tokens.css:141-142`), so opening any panel currently drops the rail's contrast to 2.12:1. The fix is
`world-hud`'s and kit-wide; this module depends on it and does not re-implement it locally.

**The right edge is shared.** The rail sits above the outliner (`world-outliner`), both right-anchored,
and §8e.1 moved the sector inspector to the **left** precisely so the two do not fight for 620px of a
1280px floor. This module owns its own height budget and yields the rest.

### 2. The flush, verbatim from the source

> *"When you press the End Turn button all notifications will be flushed, except those which prevent
> you from ending the turn."* — Endless Space 2 manual, confirmed from the primary PDF.

That rule is elegant, it is what prevents the Stellaris failure where a feed accumulates until players
dismiss without reading, and it is adopted exactly:

- On a successful commit, the rail empties.
- **Blockers survive.** A blocker cannot be dismissed and does not flush.
- **Dismissing removes an item from the rail, never from history.** The turn report is the record; the
  rail is only the feed. `world-playback` holds the record and a dismissed item is still there.
- The player never inherits yesterday's feed — **which is exactly why today's is worth reading.**

The commit signal is `world-turn`'s: `WorldTurnCommitDto.Advanced` is true on the one commit that
stepped the world. The flush fires on that, not on the button press, because a commit that did not
advance (a resend, a barrier still waiting) has not ended a turn.

### 3. Five rail-item states, and opening is not dismissing

| State | What it looks like | The rule it encodes |
|---|---|---|
| **Unread** | Bright left rule, a dot beneath the glyph, bold title | **Three channels; colour is only one of them** (GG-27, GG-30) |
| **Opened** | Dot gone, weight drops, body opens with its actions | Opening and dismissing are **two gestures with two outcomes** |
| **Dismissed** | Leaves the rail, with an undo in its place | Removed from the feed, never from the record |
| **Minimized** | One line: glyph plus title, no body, no actions | A **per-category** state, not a per-message one — where a whole category lands once the player routes it here |
| **Blocking** | **No close control.** Channel control shown but **locked** | The player learns the rule rather than wondering why the switch did nothing (GG-55) |

The locked-but-visible channel control on a blocker is the small detail that makes the model
teachable: hiding it would make the blocker look like a bug.

### 4. Categories are a closed list, and the top tier is already named

A category is what a channel setting applies to. The loam vocabulary names the top tier without
anybody having to invent a priority scheme:

| Category | Default channel | Source |
|---|---|---|
| **A part of your territory cannot pay its keep** | Toast | `loam.shortfall:` — the empire-is-fine-while-half-of-it-starves case |
| **Ground will be released next turn** | Toast | `WillReleaseNextTurn`, and the forecast shares `LoamForecast.Weakest` with the phase that takes the ground (`LoamForecast.cs:19` ← `LoamPhases.cs:138`), so warning and event **cannot disagree** — which is what licenses stating it bluntly |
| **A legion will run out of supply** | Toast | `legion.runway:` |
| **Battle and skirmish results** | Rail | kind `battle` — and see §5 |
| **Supply topped up / cut / restored** | Rail | `legion.topup:`, `supply.cut:` |
| **Ground grew, a structure finished** | Rail | `build.started:`, Growth-phase entries |
| **New places on your map** | Rail | `intel.new:` |
| **An order was refused** | Rail | kind `command.dropped` |

Everything below the top tier **starts on the rail and has to earn a promotion.** A new category
arriving on Toast by default is a spec change, not a code change.

Categories map from `world-playback`'s table — one vocabulary, two consumers. This module does not
parse an engine token; it reads a translated keyframe and its category.

### 5. Blocking is `world-turn`'s list, and this module renders it

The hard-block list **defaults to empty** and every addition is argued. The precedent is exact and it
is a retraction: ES2's manual held up a battle notification as *the* canonical hard blocker — *"a
battle notification will prevent you from completing the turn until the battle is resolved"* — the
community called it *"a feature, not a bug"*, and Amplitude eventually patched it out: *"Battle Result
Notifications no longer block the turn."*

So **battle results are a rail category here, by default and on purpose.** The strongest candidate in
our own game — *"ground goes tonight"* — is still a **nag on attempt**, not a block.

GG-53 and D6 govern the boundary: exactly one class of event may take a blocking layer unprompted, and
D6 declares it **run-ending results only** — everything else reports at band 4 and queues. A world
notification is never run-ending, so **no notification in this module may open a band-3 layer by
itself.** A toast may carry a button that opens one; that is the player asking.

### 6. The channel control lives *on* the notification — Amplitude's own correction

ES2 put the setting only in the options menu (*"in the Options menu of the game, you can select which
notifications automatically pop up and which ones stay minimized"*). Humankind's Vitruvian update
moved it onto the offending notification itself. **Take the corrected version**, for a reason worth
stating: the moment a player wants to change this is the moment one is annoying them — not later, in
a menu, from memory, with the category's name half-remembered.

- **On the notification** — *"Show skirmish results as… Toast · Rail · Off"*. Three channels, one
  exclusive choice, applied **to the category and not to this one message**. The category is named in
  the sentence so the scope of the change is never in doubt.
- **In settings** — the same list, later. This is the only place to find a category you have already
  silenced, so it must be complete, including locked (blocking) categories with their reason.

These are **player settings, not tunables**: the category→channel map lives in persisted UI settings
alongside the tooltip lock gesture. What events hard-block the turn does **not** — that is a declared,
reviewed list, lint-enforced, and deliberately not a config row a balance pass can widen by accident.

### 7. The per-turn click budget is an acceptance criterion, written as one

A quantified audit of Endless Legend's turn boundary is the most useful artifact in the corpus and it
is an indictment: *"Endless Legend makes you click 4 times for each notification (minimize the
interruption, click to open, click to act, right-click to dismiss)… a single press of the space bar
could have accomplished the same thing."*

| Doing this… | Endless Legend | Ours | How |
|---|---|---|---|
| Acknowledge one routine event | 4 clicks | **0** | It lands on the rail and flushes with the turn. It was never an interruption |
| Act on one important event | 4 clicks | **1** | The toast carries its own action button |
| Clear the feed | 1 per item | **0** | End Turn flushes it; blockers survive by design |
| Change how a category notifies you | options menu | **1** | On the notification that annoyed you, at the moment it did |

These four rows are asserted by test (§Testing 4), counting real interactions rather than being
asserted in prose. They are this module's only quantitative gate and they are the reason §4.7's
borrowings are worth the work.

### 8. What the shipped toast is missing

Two things, both small, both required:

1. **An action button.** `ToastEntry` is `{ id, tone, title, message? }` (`toastStack.ts:5-10`) and
   `Toasts.tsx:33-40` renders exactly a title and a message. §7's one-click row needs an optional
   `action: { label, run }`, and the container's `pointer-events-none` already has the matching
   `pointer-events-auto` on the card (`Toasts.tsx:27`), so a button inside it works with no layout
   change.
2. **A category**, so the channel control has a subject and so a silenced category never reaches the
   stack at all.

Both are additive to a shared shell component. The rest of `toastStack.ts` — timers, cleanup on
dismiss, `clear()` — is reused unchanged, and `clear()` is what the End Turn flush calls for the toast
half.

## What stays out

- **The End Turn control, the unresolved count and the hard-block list itself.** `world-turn` owns all
  three; this module renders a blocker's rail item and reads the commit signal.
- **The band model.** §8d.3's "band 1 is exempt from a band-2 scrim" is a kit-wide amendment to GG-5's
  table, owned by `world-hud` and recorded in `game-gui-principles.md`. This module depends on it.
- **The translation.** `world-playback` owns the sentence; this module owns the class, the channel and
  the timing.
- **The outliner.** `world-outliner` shares the right edge and owns the space beneath the rail.
- **Notifications on other stages.** The toast stack is shared; the **rail and the category model are
  the world stage's** until a second stage asks for them, at which point they move to `shell/` — a
  move, not a rewrite, which is why the rail state is a pure store from the start.


### GG-50 — this surface's volume declaration

**Tier-1 gate, and it was missing from all fifteen specs until the 2026-09-03 audit.** `ui/volumeMatrix.test.ts`
is an *exhaustive* registry — its last test is `expect(COLLECTION_SURFACES).toHaveLength(8)` — so a new
collection surface that does not register **turns a shipped test red**. Registration is not optional
paperwork; it is how this program lands without breaking CI.

| Surface | `World notification rail` |
|---|---|
| Strategy | **`render-all`** |
| Reason | Structurally bounded by two independent limits, not by hope: the rail **flushes on End Turn except blockers** (the ES2 rule this module adopts), so it cannot accumulate across turns; and the design caps the visible stack at three with the remainder behind a badge. A feed that empties every turn has no unbounded path |
| Proof | The click-budget test's fixture at the busiest turn the §8e.3 target can produce, asserting the visible count stays at the cap |

## Commands

```powershell
cd web\fusion-rpg-web
npm test                 # vitest run
npm run build            # tsc --noEmit && vite build
npm run lint
```

`shell/bandGuard.test.ts` already fails a surface that declares its own `z-index`, so a rail that
tried to float outside band 1 fails `npm test`.

## Project structure

```
web/fusion-rpg-web/src/
  shell/
    toastStack.ts          → ToastEntry gains `action?` and `category?` (additive)
    Toasts.tsx             → renders the action button
  stages/world/notify/
    categories.ts          → the closed category list + default channels (§4)
    notifyRail.ts          → pure store: items, five states, flush(except blockers)
    notifyRail.test.ts
    NotifyRail.tsx         → band-1 right edge, internal scroll (GG-61)
    RailItem.tsx           → the five states
    ChannelControl.tsx     → on the notification; locked variant for blockers
    channelSettings.ts     → persisted category→channel map
    channelSettings.test.ts
    clickBudget.test.tsx   → §7's four rows, counted
```

## Code style

The rail is a pure store; the item states are a union, not a bag of booleans; the channel map is data.

```ts
export type RailItemState = "unread" | "opened" | "dismissed" | "minimized" | "blocking";

export type RailItem = {
  id: string;
  category: NotifyCategory;
  /** Already translated by world-playback. This module never sees an engine token. */
  title: string;
  body: string;
  state: RailItemState;
  /** Blockers cannot be dismissed and do not flush — see spec §2. */
  readonly blocking: boolean;
};

/** End Turn flush. The one rule, in one line, so it cannot drift. */
export const flush = (items: RailItem[]): RailItem[] => items.filter((i) => i.blocking);
```

## Testing strategy

Vitest, colocated. Five levels, and the last is unusual for this repo and deliberate.

1. **The flush rule** — commit with a mixed feed and assert only blockers remain. Then assert the
   dismissed items are still retrievable from the turn report, because *"dismissed removes it from the
   rail, never from history"* is a promise and not a phrasing.
2. **Commit vs. press** — the flush fires on `advanced === true`, not on the button. A commit that
   does not advance leaves the rail alone. This is the test that catches the obvious wrong wiring.
3. **The five states, each asserted by a non-colour channel** — unread carries a dot **and** bold
   weight **and** a rule; blocking has **no close control** and a **locked, visible** channel control.
   Queried by role and accessible name, never by class.
4. **The click budget** — four tests, one per row of §7, counting real `userEvent` interactions:
   acknowledge a routine event = 0, act on an important one = 1, clear the feed = 0, change a
   category's channel = 1. A regression here is a design regression and this is the only way to notice
   one.
5. **Category discipline** — every category in `categories.ts` has a default channel, and a test
   asserts **no category defaults to Toast unless it is in the declared top tier** (§4). Adding a
   Toast default is then a visible diff on that list rather than a quiet line in a component.

Plus one guard-shaped assertion: **no code path in this module opens a band-3 layer.** GG-53's lint
covers band-3 openers repo-wide; this module asserts its own compliance so D6's "run-ending results
only" is not re-argued per feature.

## Boundaries

- **Always:** default a new category to the rail; put the channel control on the notification as well
  as in settings; flush on `advanced`; keep blockers undismissable; carry every state in more than one
  visual channel.
- **Ask first:** promoting a category to Toast by default. Adding anything to the **hard-block list**
  — that is `world-turn`'s declared list and a reviewed change. Moving the rail out of band 1, which
  would re-open §8d.3.
- **Never:** open a band-3 layer from a notification (GG-53 / D6 — run-ending results only, and a
  world notification is never one). Never let a blocker be dismissed or flushed. Never show an engine
  token — the rail consumes translated text only. Never make the rail scroll the stage (GG-61: it
  scrolls inside its own bounded shell).

## Success criteria

1. Two classes exist with the boundary in §1, and the toast stack is the shipped one plus an action
   button and a category — not a second implementation.
2. End Turn flushes the rail **except blockers**, fires on `advanced`, and a dismissed item is still
   in the turn report.
3. All five rail-item states render, each distinguishable without colour, with the blocker's channel
   control visible-and-locked.
4. Category→channel is player-configurable **from the notification** and from settings, persisted, and
   the two lists cannot disagree.
5. The four click-budget rows in §7 are asserted by counted-interaction tests and hold.
6. No category defaults to Toast outside the declared top tier, proven by a test over the list.
7. Nothing in this module opens a band-3 layer.
8. `npm test`, `npm run build` and `npm run lint` are green.

## Open questions

**None.** §4.7 decided the two classes and the flush; §8c.1 confirmed the volume-dependent controls
once §8e.3 fixed the legion target at 6–10; the channel-control placement follows Amplitude's own
correction and the hard-block default is empty by §4.6.
