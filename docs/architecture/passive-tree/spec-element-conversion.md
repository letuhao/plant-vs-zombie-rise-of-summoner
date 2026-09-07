# Spec: `element-conversion`

Module of [passive-tree](../passive-tree-map.md), owned here because [`tree-binder`](spec-tree-binder.md)
D16 is the capability that needs it — the same relationship [base-defense/spec-siege-construction.md]
(../base-defense/spec-siege-construction.md) has to the atom vocabulary it extends. Amends the shared
atom vocabulary, so `decisions.md`, [`effect-atom/atom-catalog-ssot.md`](../effect-atom/atom-catalog-ssot.md)
and [`effect-atom-map.md`](../effect-atom-map.md) all move with it, per those files' own stated rule
("growing this list is a reviewed change to this row").

> **Reads [`effect-atom/definitions.md`](../effect-atom/definitions.md)** and
> [`effect-atom/atom-catalog-ssot.md`](../effect-atom/atom-catalog-ssot.md) — where this spec and
> those disagree, they win.

**D56 (owner, 2026-09-06):** *"why don't we make spec to cover it?"* — a real spec, in answer to
tree-binder's own refusal (§7.2), not a prioritize-or-defer call on an open question.

## 0. ⛔ The count this spec corrects on arrival

Every existing citation of *"the 17th atom kind"* for this capability — `spec-tree-binder.md` §7/§7.2/
§7.3/Boundaries/D16 (five sites), `decisions.md:112` cited by name — is **stale as of today, for a
reason unrelated to this spec's own subject.** Verified this session, by reading
[`atom-catalog-ssot.md`](../effect-atom/atom-catalog-ssot.md) §2 ("The closed kind list — **17**") and
`AtomKindRegistry.cs:36` (`KindCount = 17`) directly: **17 kinds and 8 attach points already exist**,
the 17th (`structure.place`, on the new `Siege` attach point) landed 2026-09-06 via base-defense's
`siege-construction` module — a change this spec's own subject had nothing to do with. None of the 17
writes an `ElementPayload` (confirmed by reading every row in the §2 table). **So the gap tree-binder
found is real and still open — it is the capability that would be the 18th kind and 9th attach point,
not the 17th and 8th.** `decisions.md:112` is also a stale line reference in the same way: the current
"Atom attach points" row is at `decisions.md:113` (`:112` is now "Action eligibility axis," an
unrelated 2026-09-03 decision that was inserted before it). Both corrections are named here once
rather than re-discovered at build time; `tree-binder`'s own five citations (§7/§7.2/§7.3/Boundaries/
D16) were updated in the same session this spec was written, cross-referencing this file by name —
not left for a later, unrelated touch.

**Also confirmed stale, `DESIGN-GATE.md` row 41 itself** ("The atom / Secondary effect layer" —
*"16 kinds"*, dated 2026-09-05): the count moved to 17 the next day (siege-construction) and the row
was never updated — exactly the *"gone stale twice"* pattern that row's own text already warns about
for itself. Fixed in the same pass as this spec (one line, `docs/DESIGN-GATE.md`), per evidence rule 6
("when you correct something, propagate it").

## Objective

A passive-tree node that reads *"convert 40% of your fire damage to ice"* needs a real writer for
`ElementPayload` — the weighted `{element, weight}` component list combat already resolves against
(`ElementPayload.cs`, `IElementHub.ResolvePayloadBonus`). Today the mechanism is real and the writer is
not: `DamagePacketBuilder.ParseElementPayload` (`DamagePacketBuilder.cs:48,89-109`) builds a packet's
`ElementPayload` from an **effect's own authored overlay** at packet-construction time — the one and
only producer in `src/`, confirmed by the 32-file reference sweep this session ran over
`ElementPayload`/`ElementPayloadComponent`. No atom kind, no attach point and no combat-dispatch read
point exists for a **permanent, passive** reweighting of an attacker's own hit. This module adds
exactly that, so a conversion node can be priced and bound instead of refused — **scoped to
redistributing an already-elemental hit's weight (§2b); converting an implicitly non-elemental hit into
a partially-elemental one is a real, separate, larger capability this spec does not attempt (§2b's own
"scope, corrected against the real type" note)**.

## 1. What exists today

| Fact | Where |
|---|---|
| `ElementPayload` — weighted components, positive weights summing to 1 within `1e-6` | `Combat/Element/ElementPayload.cs:3-37` |
| The only producer: parses an effect overlay's own `elementPayload` field at packet build | `DamagePacketBuilder.cs:48,89-109` |
| The only consumer of the built payload: resolves a damage bonus/penalty against defender resistances | `IElementHub.ResolvePayloadBonus` (`IElementHub.cs:12-15`), read via `OverlayCombatCalculator.ParseComponents(packet.ElementPayload)` at `CombatDamageDispatcher.cs:58` |
| `tree-binder`'s own refusal for a conversion slot, already shipped | `spec-tree-binder.md` §7.2 — *"no kind among the [17] writes an element payload; no Element attach point exists"* |
| The mechanism CLAUDE.md's own three-question ladder asks for, already answered by `tree-binder` §7 | *"Does the RPG layer already have a channel/atom/runtime for conversion? No — the mechanism exists, the writer does not"* (`spec-tree-binder.md`, applying CLAUDE.md's ladder directly) |

**Not a "damage rider."** `atom-catalog-ssot.md` §2's own "Not kinds, on purpose" table already
rejects a dedicated kind for *"a trigger plus `resource.delta` with an element payload"* — that is a
one-shot, effect-authored hit (a fireball proc), which `DamagePacketBuilder`'s existing overlay path
already covers today. A tree-binder conversion node is the opposite shape: a **permanent**, passive
modifier on the actor's *own default* attack, present on every hit for as long as the node is owned —
structurally the same shape `stat.derived` and `bullet.modify` already are (`atom-catalog-ssot.md` §2
row: *"stat.derived and bullet.modify carry no trigger at all... permanent modifiers; apply and revert
are runtime lifecycle, not content"*). This is why the existing rejection does not cover this gap.

## 2. The contract

### 2a. The new attach point — `Element`

**Ninth attach point**, alongside the existing `Stat · Resource · Status · Shield · Board · Match · Ui
· Siege` (`AtomKind.cs:8-43`). `AttachPointCount` moves **8 → 9**.

**`tree-binder` §7.1 itself floated a cheaper shape and explicitly deferred the call**: *"the cheapest
shape is a [kind] on the existing `Board` or `Stat` attach point rather than an eighth attach point —
but that is a decision for whoever specs it, not for this module."* This spec is that decision, and it
goes the other way, for a reason specific to each alternative:

- **Not `Stat`.** A conversion writes a combat-math **input structure** (a weighted list), never a
  channel value — `Stat` is a channel value by definition (`AttachPoint.Stat`'s own doc comment).
  Forcing a weighted-component write through a scalar-channel seam is the exact "smuggled in as a
  wiring gap" `spec-tree-binder.md`'s own DESIGN-GATE checklist already refuses (§7's own line: *"named
  explicitly as a NEW CAPABILITY requiring a reviewed decisions.md change — never smuggled in as a
  wiring gap"*).
- **Not `Board`.** Every existing `Board`-attached kind acts **on a cell or an entity within the
  running match** (`spawn.entity`, `board.action`, `grid.spawn`, `grid.clear`, `box.set`, `bullet.modify`
  — `atom-catalog-ssot.md` §2's own line: *"a `board` kind acts on a cell or an entity... each takes
  `row`/`col`"*). A conversion names no cell and no board entity — it reweights the ATTACKER'S OWN
  in-flight payload, the same "no cell" shape that already justified `Match` as `Board`'s sibling
  rather than a reuse (`atom-catalog-ssot.md` §2's own `match`-vs-`board` distinction, restated at §84
  of that file). Reusing `Board` here would be the identical category error `Match` was created to
  avoid, one attach point later.

A new, single-purpose attach point costs one enum member and one line in three already-shipped
guard tests (`AtomKindRegistryTests.cs`'s count assertions) — the same price `Match`, `Ui` and `Siege`
each already paid for the identical reason.

### 2b. The kind — `element.convert`

**Eighteenth kind.** `KindCount` moves **17 → 18**.

| Field | Value |
|---|---|
| `KindId` | `element.convert` |
| `Attach` | `AttachPoint.Element` |
| `Params` | `fromElement` (optional — omitted means *"any component currently in the payload, largest first"*), `toElement` (required, an `ElementTypeId`), `shareMilli` (required, per-mille of the **affected component's own weight** moved, `1..1000`) |
| `Support` | **Corrected during the build, 2026-09-07 — was Lawn ✅ / Battle ✅ / Sim `Partial` as originally written here; shipped as Lawn `None` / Battle `None` / Sim `None` (fully quarantined) instead.** The original claim assumed the FOLD MECHANISM's own theoretical capacity (`BattleStatComposer` already folds bound permanent atoms structurally) was the same thing as a REAL, WORKING reader existing today — it is not: no code anywhere resolves an actor's bound `element.convert` grants into anything, on any runtime, because the combat-dispatch read point (§2b, below) is genuinely unbuilt. `ParamParityGuardTests` caught this directly (a kind claiming real support with zero consumer files mapped) the same session it shipped. Quarantined per `AtomKindRegistryTests`' own established pattern ("a kind may sit in the vocabulary ahead of its consumer, but it must be quarantined... never advertising support it does not have") — mirrors `stat.derived`'s own D6 quarantine history exactly. Flip each runtime to a real state only once ITS OWN reader/executor actually exists, never assumed from a sibling runtime or from the fold mechanism's theoretical capacity. |
| `Triggers` | None — permanent modifier, no trigger allowed, none required (the `stat.derived`/`bullet.modify` shape, `atom-catalog-ssot.md` §2's closing paragraph) |
| `Categories` | `PowerCategory.Offense` |

**Read point, proposed and named as a design decision, not yet built:** `CombatDamageDispatcher`'s
existing `onDamageApplied` seam (`CombatDamageDispatcher.cs:24,57-58`) already demonstrates the
pattern this kind needs — *"a new consumer reads an existing field, nothing upstream changes"* (the
same comment on that exact line names this as the established shape for this class of extension). The
natural insertion point is **before** `DamageApplyPipeline.ApplyPacketToFunnel` (`:47-48`): resolve the
attacking actor's own bound `element.convert` atoms (the same "resolved value at point of use" read
`stat.derived`/`bullet.modify` already use, never event-fired) and reweight `packet.ElementPayload`.
**This exact call site is this spec's one open engineering question, not a settled fact** — named here
so a build session starts from a proposal to verify, not a blank page, but not asserted as already
true.

**⛔ Scope, corrected against the real type — a conversion redistributes an EXISTING payload, it never
fabricates one.** `ElementPayload.Validate` (`ElementPayload.cs:22-37`) requires weights to sum to
**exactly** 1.0 and rejects any component at `Weight ≤ 0`; `ElementTypeId` (`ActorElementTypes.cs:3-11`)
has exactly six members — Fire, Ice, Air, Earth, Light, Dark — **no "Physical"**; and
`ActorElementTypes.Neutral.Primary` is `null`. A hit with **no** `ElementPayload` at all (the packet
field is nullable — `CombatDtos.cs:115` — and that is how a purely mechanical, non-elemental attack is
represented today) has no component for `element.convert` to read a share *from*, and there is no
"Physical" element to assign the untouched remainder *to*. **So `element.convert` only fires against a
payload that already carries at least one real elemental component** — it redistributes weight between
the 6 real elements (e.g. *"convert 40% of your fire damage to ice"*), never converts an implicitly
non-elemental hit into a partially-elemental one. On a `null` payload, `element.convert` is a **no-op**
for that hit — not an error, not a fabricated base, just nothing to do (§3 names this explicitly).
**Widening `ElementPayload` itself** (e.g. relaxing the sum to "at most 1.0," with the unaccounted
remainder implicitly non-elemental) would fully deliver `tree-binder`'s own literal *"convert 40% of
your damage to ice"* wording even for a currently-non-elemental attacker — but that touches shared
combat-core math (`IElementHub.ResolvePayloadBonus`, `OverlayCombatCalculator.cs:128-172`) this spec has
not audited for correctness under a partial-sum payload, and is explicitly **out of scope here**: a
follow-up spec's job if the no-op case above turns out to matter in practice, not a silent scope
expansion into a file this spec does not otherwise touch.

### 2c. Order of composition — multiple conversions on one actor

Applied in `nodeKey` ordinal order (deterministic, matches `tree-plan`'s own R3 minting order — no new
tie-break invented), against the **existing payload only** (§2b). Each conversion moves `shareMilli` of
the **named (or, if `fromElement` is omitted, the single largest remaining) component's own current
weight** to `toElement` — never more than that component actually holds, so two nodes each reading
*"convert 100%"* of the SAME `fromElement` cannot double-count it (the second finds nothing left to
convert and is a no-op for that component). A component whose weight is driven to exactly zero is
**removed from the list, not kept as a zero-weight entry** — `ElementPayload.Validate` rejects
`Weight ≤ 0`, so a full (`shareMilli=1000`) conversion must drop the source component rather than retain
it at `0`. This mirrors D40's own `nullification`/exclusion discipline: the LAST rule to fire on a
shared resource wins the remaining share, never an additive stack past the resource's own total.

### 2d. Pricing — this module owes tree-binder nothing new

The refusal `tree-binder` §7.2 already ships is the correct behavior **today** and stays correct after
this spec, until the kind is reviewed and built — R-G1's own shape (`tree-plan` §7.1): a capability
without a production carrier refuses rather than substitutes. Once `element.convert` exists in
`AtomKindRegistry`, `tree-binder`'s own §7 refusal clears itself with **zero code change in
`tree-binder`** — the refusal already keys on "does 18 kinds admit a writer," never on a hardcoded 17.

## 3. What it must NOT do

- Write to `Stat`, `Resource`, `Status`, `Shield`, `Board`, `Match`, `Ui` or `Siege` — a conversion
  touches `ElementPayload` alone, never a channel, a resource, a status, or Unity/board state.
- Fire from an event/trigger — permanent modifiers only, matching `stat.derived`/`bullet.modify`.
- Fabricate a component on a `null` `ElementPayload`, or invent a "Physical" `ElementTypeId` — neither
  exists in the real type (§2b). A hit with no payload is a no-op for `element.convert`, not an error
  and not a manufactured base.
- Leave a zero-weight component in the resulting payload — `ElementPayload.Validate` rejects
  `Weight ≤ 0` (§2c); a fully-converted source component is removed from the list, never kept at `0`.
- Let a conversion exceed 1000‰ of a hit's total weight across all of an actor's bound conversions
  (§2c) — a silent over-conversion is the same "changed only the number, created a dead stat" defect
  D16 already names for the writer's absence.
- Assume `Sim` support without proving it against the real fold, the same `Full`-vs-`Partial` empirical
  bar `stat.derived`'s own re-opening already set (`decisions.md` "Derived-write lawn executor" row,
  decision 2: *"decided from the built executor, not up front"*).
- Touch `AttachPointCount`/`KindCount` without updating `atom-catalog-ssot.md` §2, `decisions.md`'s
  "Atom attach points" row, and `DESIGN-GATE.md` row 41 in the **same** change — the exact propagation
  failure this spec's own §0 just found and fixed once already.

## 4. Testing strategy

Follows `AtomKindRegistryTests.cs`'s own existing shape (self-consistency counts, never copied
literals) plus a new `Combat.Element` suite:

- `AttachPointCount == Enum.GetValues<AttachPoint>().Length` and `KindCount == AtomKindRegistry.All.Count`
  (already-shipped guards; this module adds no new guard, it makes the existing ones report 9/18).
- A conversion atom with `fromElement=Fire,toElement=Ice,shareMilli=400` against a payload of
  `{Fire: 1.0}` produces exactly `{Fire: 0.6, Ice: 0.4}`.
- `shareMilli=1000` (full conversion) against `{Fire: 1.0}` produces exactly `{Ice: 1.0}` — `Fire` is
  **removed**, not retained at weight `0` (§2c; would otherwise throw in `ElementPayload.Validate`).
- A conversion atom against a `null` `ElementPayload` is a **no-op** — the packet's payload stays
  `null`, no exception, no fabricated component (§2b, §3).
- Two conversions on the same `fromElement` never move more than that component's own current weight
  in total — the second, applied after the first has already zeroed or reduced the source, converts
  only what remains (§2c), proven over an adversarial ordering (reverse `nodeKey` order still produces
  a valid, sum-to-1-or-less-only-via-removal payload, never a negative or over-1000‰ weight).
- `element.convert` carries no trigger — `AtomRowValidator` rejects one authored on it, matching the
  existing `stat.derived`/`bullet.modify` rejection tests.
- The permanent-modifiers guard set (`atom-catalog-ssot.md` §2's own citation,
  `AtomKindRegistryTests.cs`'s `permanentModifiers`) gains `element.convert` as a third member.
- A hand-simulated `Sim` fold is run against the real `ActorDerivedLookup`-style fold **before** this
  kind's `Support.Sim` cell is set to anything but `None` — the same empirical gate `stat.derived`'s
  own Sim re-opening required, never assumed from the Lawn/Battle result.

## 5. Boundaries

**Always:** treat `ElementPayload` as the sole write target; keep the kind trigger-less; keep the
attach point's own doc comment as precise about scope as `Ui`'s and `Siege`'s already are; update
`atom-catalog-ssot.md` §2, `decisions.md`, `effect-atom-map.md`, and `AtomKindRegistry.cs`'s own
`KindCount`/`AttachPointCount` constants in the same change that adds the kind.

**Ask first:** the exact combat-dispatch read-point call site (§2b's named open question) — a real
engineering decision with more than one defensible answer (before `ApplyPacketToFunnel` vs. inside
`DamagePacketBuilder.FromOverlay` itself vs. a new stage entirely), owned by whoever builds this, not
pre-decided here; whether `shareMilli` should be a flat per-mille or itself a `Θ`-scaled `ValueSpec`
(D24's "coefficient, not magnitude" rule may apply here exactly as it does to every other tree-binder
node — not resolved in this spec because it is a `tree-binder`/`tree-plan` pricing question, not an
atom-vocabulary one).

**Never:** let this module's own writer touch a channel, resource, status or board state; assume
`Sim` support without the same empirical proof `stat.derived` required; let a second conversion module
(if one is ever proposed for a different actor category) bump `KindCount`/`AttachPointCount` in
isolation without checking this row first — the exact `spec-ui-attach-point.md` §6 count-collision
hazard applies here too, and as of this spec's writing there is at least one other in-flight,
uncommitted change (`data/seed/derived-stats/catalog.json`'s `loadout.slots` family, per
`decisions.md`'s "extended action slots" row) independently considering *"a `stat.derived` on
`loadout.slots` if the closed 16-kind vocabulary admits it, else a reviewed seventeenth kind"* — that
row's own count is now doubly stale (16 was already 17 when it was written, and would be 19 if both
proposals land) and should be corrected the next time it is touched, but is explicitly **not** edited
by this spec, matching this file's own historical-record convention (§0).

## 6. Dependencies and cross-program hazards

| Item | Detail |
|---|---|
| **`extended action slots` decision (`decisions.md`, 2026-09-05)** | Independently floats *"a reviewed seventeenth kind"* for `loadout.slots` if `stat.derived` cannot carry it. Same `KindCount`/`AttachPointCount` collision `spec-ui-attach-point.md` §6 already named for E35/E37 — whichever of the two lands second must edit the guard to the combined total and say so in its own commit. Not this spec's call to sequence; named so neither session discovers the collision by a failing guard test |
| **`tree-plan`** | Owes the pricing question (§5, Ask first) once this kind exists — D24's coefficient-not-magnitude rule needs a real answer for `shareMilli`, analogous to every other node's `budgetShareMilli` |
| **`tree-binder`** | Its own §7.2 refusal clears with zero code change once `element.convert` is registered (§2d) — no coordination needed beyond the registry landing |
| **Combat program (`CombatDamageDispatcher`/`DamagePacketBuilder`)** | Owns the actual read-point wiring (§2b) — this spec proposes, does not mandate, the insertion call site |
| **`AtomImporter` staleness** | This module is registry code, not seed data — the importer will report "nothing changed," matching `spec-ui-attach-point.md` §6's own already-recorded note for the identical situation |

## Success criteria

**All six BUILT + VERIFIED 2026-09-07 — see `tasks/passive-tree-todo.md` task J11 for the full evidence
trail (13 new tests, all four cross-doc updates, a real spec-vs-code gap found and fixed in §2d's own
"zero code change" claim).**

- [x] `AttachPointCount = 9`, `KindCount = 18`, both self-consistency-guarded, never a copied literal.
- [x] `atom-catalog-ssot.md` §2's table gains row 18; `decisions.md` gains an "Atom attach points"
      amendment naming `Element`; `effect-atom-map.md` names the module; `DESIGN-GATE.md` row 41
      reads 18/9.
- [x] `tree-binder`'s §7.2 refusal, re-run against a fixture conversion node, no longer refuses —
      proven by test, not by inspection. **Needed one real code change** (removing
      `AffixComposer.ParseAtom`'s own hardcoded "contains 'convert'" string check, which keyed on the
      literal substring rather than registry membership) — §2d's own "zero code change in tree-binder"
      claim did not hold as written; corrected in the same session that found it.
- [x] A conversion node's `ElementPayload` output is exact (§4's payload test), never approximate.
- [x] Two stacked conversions never exceed 1000‰ total share (§2c), proven adversarially.
- [x] `Sim` support is whatever the empirical fold test (§4) finds, not what this spec assumes — no
      fold test was run, so it ships `None`. **Widened during the build, same day**: `Lawn`/`Battle`
      also ship `None`, not the `Full`/`Full` this criterion originally implied would need only Sim
      left honest — `ParamParityGuardTests` found no real reader exists for ANY runtime yet (the
      combat-dispatch call site, §2b, is genuinely unbuilt), so all three are the honest "not proven"
      default, quarantined together, never assumed from the fold mechanism's own theoretical capacity.

## Open questions

Two, both real, both named already in §5/§6 rather than answered here:

1. **The combat-dispatch read-point call site** (§2b) — an engineering decision for whoever builds
   this, with a proposed default (before `ApplyPacketToFunnel`) but no owner ruling needed today; R-G1
   already covers the interim (refuse until built).
2. **`shareMilli` as a flat per-mille vs. a `Θ`-scaled coefficient** (§5) — a `tree-plan`/`tree-binder`
   pricing question, not an atom-vocabulary one; this spec defines the kind's params shape either way
   (`shareMilli` is the wire field regardless of whether its *source* is a flat author-set constant or
   a resolved coefficient).

## Decisions implemented

| Requirement in this spec | Decision |
|---|---|
| Objective, §1 | **D16** — `tree-binder`'s own naming of the gap |
| §2a/§2b | **D56** (owner, 2026-09-06) — write a real spec, not a priority call |
| §2d | Existing R-G1 (`tree-plan` §7.1), applied without modification |
| §3, §5 | Existing atom-vocabulary boundaries (`atom-catalog-ssot.md` §0's own "richness from families ×
tiers × containers, never from growing the kind list" — this IS a reviewed exception, not a precedent
for a second one) |
