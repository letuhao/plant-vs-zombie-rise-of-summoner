# Action choice — which held action actually fires — the ideal

**Status:** idea phase, 2026-09-13 (enrichment round, correcting an earlier premature deferral in
[action-playability-ideal.md](action-playability-ideal.md)). Not a spec. No build authorized. Does not
reopen any of [action-ideal.md](action-ideal.md)'s 26 sealed decisions.

## Which loop this extends

[the-loops.md](../guide/the-loops.md) **Idle expeditions** ("dispatch → wait → collect," shipped,
auto-resolve) and **Farming/hunting/defending the empire** (sieges, `DistrictAssaultResolver`, also
auto-resolve) are today's *only* shipped consumers of a real battle. **Combat depth** and **Level up and
power** (the unlock ladder, the five-slot loadout) are the systems whose entire payoff is measured through
those two auto-resolved consumers, because **Dungeon crawler — the Delve**'s "interactive battles you play
yourself" is still WIP. Correction to the earlier ideal's loop framing: this is not primarily a
Delve/interactive-battle concern. It is a shipped-consumer concern, today, for the two loops that already
ship.

## Why this got re-opened

The prior round (`action-playability-ideal.md`) named this gap (there, "A31") and recommended deferring it:
*"scoring a choice among skills nobody can hold yet is premature."* **The owner's counter is correct and
changes the priority, not just the wording**: because this game is automation-first — expeditions and
sieges resolve with no human turn-by-turn input — *which action fires* is not AI polish layered on top of
a human-played game. It **is** the mechanism, full stop, by which a player's level-ups, unlock-ladder rolls,
and five-slot build choices ever turn into a different outcome. If it stays broken, A26 (un-gating the
ladder) makes a specimen *hold* more real actions without that ever being *visible* in a result — the
exact "provably correct, provably pointless" shape this program has already found and fixed once
(action-map.md §14: `ExecuteSummon` granting zero actions was correct-but-gapped for a different reason;
this is the live-outcome-side mirror of that).

## Step 0 — principles restated

1. **RPG layer, not PvZ.** Action choice runs entirely in `Core/Actions` and `Core/Battle`, server-side,
   battle-mode only. It never touches Unity, the lawn, or a Writer surface — trivially gameless-first
   compliant, since it has no lawn dependency to begin with.
2. **One power ladder, no private curve.** `CompiledAction.Rung` (and the rung table's already-priced
   `q(rung)` from action-ideal.md §4.1–4.2) is the *only* magnitude this may read to compare two actions'
   worth. **Reading it is not inventing a second ladder** — it is the ladder already computed at compile
   time, for a different purpose (cost/cooldown/structure), reused for ordering. Inventing a fresh
   "action score" formula independent of rung would be exactly the private `f(level)`/`f(power)` this
   repo's SSOT exists to prevent.
3. **The balance surface is data.** Anything this work adds as a genuinely new number (not a reuse of
   `Rung`) belongs in `data/tuning/`, not a `const`. The recommended shape below adds none.
4. **SOLID / no parallel path.** The fix is a change *inside* the existing `IIntentSource` seam
   (`StubIntentSource`, already proven swappable — `SiegeAiIntentSource` already replaces it for
   target-selection in production) and the existing `ActionTagPreference` ordering function. It is not a
   second selection mechanism living beside them.

## What already exists

### Built

| Piece | Evidence |
|---|---|
| A swappable selection seam, proven in production | `IIntentSource`, `IBattleView`. `SiegeAiIntentSource.cs:167-281` already replaces `StubIntentSource` for *target* selection in real siege battles (`DistrictAssaultResolver.cs:151-162`) — the architecture for "smarter AI drops in without a kernel change" is not hypothetical, it already shipped once, for the other half of the same decision |
| A priced, comparable magnitude per action | `CompiledAction.Rung` (`ActionCompiler.cs:54,64`, from `RungTable`) — action-ideal.md §4.1's `q(rung)` is already a monotonic, machine-checked ladder (E9's own monotonicity assertion, T5). Comparing two held actions by `Rung` is comparing two already-priced numbers, not computing a new one |
| A condition/predicate evaluation already run per candidate | `UsabilityEvaluator.Evaluate` (`StubIntentSource.cs:68-71`) already evaluates the action's own gate-6 condition (E3 predicate tree) for every held action, every decision — the information is computed, just discarded as a bare `IsUsable` bool once true |
| The explicit acknowledgment that today's order is a placeholder | `ActionTagPreference.cs:3-8`, its own doc comment: *"the rest of the ranking below is a decided-now placeholder... the exact ranking is content to rebalance once a real stub AI plays real matches."* This was never claimed as the final answer |

### Wiring gap — the concrete, provable defect

**Two actions that share a tag rank tie-break on `action_id`, alphabetically** (`ActionTagPreference.cs:47`:
`string.CompareOrdinal(a.ActionId, b.ActionId)`). Concretely: if a real specimen holds two Offensive-tagged
actions — say a rung-1 plain strike (`atk.basic_strike`) and a rung-9 conditional combo skill built from
the unlock ladder (`atk.zzz_combo`) — **the plain strike always fires**, every single decision, forever,
regardless of level, rung, or whether the combo's condition is live, purely because `"atk.basic_strike" <
"atk.zzz_combo"` as a string. The five-slot loadout, in automated play, degenerates to "whichever
same-priority action's id sorts first" — a property of *naming*, not of *build*.

**This is provably not cosmetic.** Every consumer that matters today (expeditions, siege) is
auto-resolved. A21's whole point was making level-up grant a real second action; this defect means that
second action can be granted, held, equipped, and priced correctly, and still **never once fire**, for as
long as its id string sorts after whatever else the specimen holds at the same tag rank — an outcome no
balance pass, playtest, or win-rate sweep would ever attribute to "the ordering," because nothing prints
`ActionTagPreference`'s tiebreak reasoning today.

### Real gap — a separable, larger piece

**Choice cannot yet see a conditional payoff's live state.** Action-ideal.md §7.3/§8.6 designed a whole
mechanism (enabler/payoff pairing, predicate-frequency pricing) around *"this action is much better right
now because the target is Chilled."* But `UsabilityEvaluator`'s gate-6 condition is the **action's own**
usability precondition — not the same thing as an individual atom's `when.predicate` inside the action's
container (E3, evaluated later at resolve/fire time, not at selection time). Nothing in `IBattleView`
today lets a selector ask *"if I use this action now, will its conditional bonus atom actually trigger"*
— that visibility does not exist at decision time. Building it is a real, separate, larger piece of work
(new `IBattleView` surface reading atom-level predicate truth ahead of resolve) and is legitimately
separable from the tiebreak fix above.

## Prior art

**WoW SimulationCraft's Action Priority List (APL)** is the closest-fitting precedent, closer than generic
utility-AI scoring: *"Action lists are priority lists: periodically [the sim] scans your character's
action list, starting with the first action (the highest priority) and continuing until an available
action is found... Actions that are not possible at the moment (cooldown not ready, conditions not met)
are considered not available and it moves to the next one."* [ActionLists — simc wiki](https://github.com/simulationcraft/simc/wiki/ActionLists) ·
[SimulationCraft/Raidbots guide](https://maxroll.gg/wow/resources/simulationcraft-and-raidbots-guide).
This is **exactly** this repo's shipped shape — an ordered list, first usable wins — the defect is only
that the *order* is meaningless past tag rank, not that the *mechanism* (ordered-priority-list rather than
per-decision scoring) is wrong. **The fix is re-ordering the list, not replacing the mechanism.**

**Commercial idle/gacha auto-battle games solve the same problem by exposing priority as a build lever,
not a hidden AI**: *"Champions use abilities based on configurable AI priorities in auto-battle mode... RAID:
Shadow Legends gives granular control over auto-battle behavior, allowing prioritization of which
abilities fire first."* [Best idle/auto-battle gacha games](https://ultimategacha.com/best-gacha-games-with-auto-battle/) ·
[Plarium: RAID vs AFK Journey](https://plarium.com/en/blog/raid-shadow-legends-vs-afk-journey/). This is
the Vision-tier follow-on (a player-authored priority order per loadout, matching this game's own "you are
not a class, build freely" stance) — not required for the immediate correctness fix, but the natural
long-term shape once a deterministic default exists to layer a player override on top of.

## The shape

**Two separable pieces, correctly sized very differently — do not defer the small one.**

**Fix 1 (small, do now, no new tunables) — order the tiebreak by `Rung`, descending, before falling back
to `action_id`.** `ActionTagPreference.Compare` changes from `(tagRank, actionId)` to
`(tagRank, -Rung, actionId)` — `action_id` stays as the final, deterministic tiebreak (needed for replay
determinism when two held actions somehow share a rung), it just stops being the *decisive* one. This is
a pure reuse of an already-computed, already-priced number (§ "one power ladder" above) — no new mechanism,
no new data file, and it directly fixes the concrete failure named above: a rung-9 unlock-ladder skill now
outranks a rung-1 basic of the same tag, every time. Buildable immediately, independent of the larger piece.

**Fix 2 (larger, real gap, separable, can follow later) — surface atom-level predicate truth to the
selector**, so a live-Chilled combo can outrank a same-rung unconditional action *specifically when its
condition is live right now* — not merely "generally worth more." This needs a new, narrow
`IBattleView` read (does this action's container hold an atom whose predicate currently evaluates true),
not a new selection mechanism — the ordered-priority-list shape from Fix 1 is unchanged, this only makes
one more fact available to break a tie inside it.

**Rejected shape: full utility-AI scoring (0–1 normalized score across hit%/damage/resource/HP,
weighted-sum or softmax).** Rejected for *now*, not forever: it needs a new tunable weight file
(`data/tuning/action-selection-weights.v{n}.json`), a calibration pass, and solves a more general problem
than the one actually broken today. The concrete defect (alphabetical tiebreak) does not need it — Fix 1
closes it with zero new tunables. Revisit full scoring only if Fix 1 + Fix 2 together are measured (via
the already-built A20 synthetic-loadout harness) and still don't move real-battle outcomes enough.

## Tunables

**Fix 1 introduces none** — it reuses `Rung`, already tunable via the shipped rung table
(action-ideal.md §4.1, `data/tuning/action-rungs.v1.json`). **Fix 2 introduces none either** — predicate
truth is a boolean read, not a number. A future full-scoring pass (rejected above, for now) would be the
first piece of this area that needs a new tuning file.

## What this deliberately does not decide

- Does not decide whether Fix 2's new `IBattleView` surface is a per-atom bool, a count, or something
  richer — that is a module-spec-level design question once Fix 1 is confirmed insufficient alone.
- Does not decide anything about a player-facing, player-editable priority order (the RAID/SimC-style
  Vision-tier follow-on) — named as prior art, not proposed as scope here.
- Does not touch target selection (already improved once, for siege, via `SiegeAiIntentSource`) — this is
  about *which action*, given a target, not *who* the target is.
- Does not reopen `ActionTagPreference`'s tag-rank ordering itself (Offensive > Debuff > ... > Construct)
  — only its tiebreak.

## Open questions

**None that block Fix 1.** It is a one-function change (`ActionTagPreference.Compare`), reusing data
already on `CompiledAction`, with a mechanical acceptance test (a planted rung-9-vs-rung-1 same-tag pair,
asserting the rung-9 one is chosen) — small enough to fold into the same closure round as A26-A30, not
deferred. **Fix 2's exact `IBattleView` shape is a spec-phase question**, not an owner decision — there is
no ambiguity to resolve here, only design work to do.

**Recommendation for the capability map**: un-defer this. Split what was "A31" into **A31
`action-choice-rung-tiebreak`** (Fix 1, small, sequence it right after A26 — it matters the moment A26
makes a second real action holdable) and **A32 `action-choice-condition-awareness`** (Fix 2, larger,
genuinely separable, can follow A31 without blocking A27-A30).
