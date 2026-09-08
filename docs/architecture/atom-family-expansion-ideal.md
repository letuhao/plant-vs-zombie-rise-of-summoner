# Atom family expansion — ideal (2026-09-07)

## 0. Architecture principles restated inline

**Every RPG feature lives in the RPG layer, never built by changing what PvZ is.** Atoms, atom
families, containers and the whole effect pipeline live entirely in `FusionRpg.Core.Effects.Atoms` —
PvZ has no notion of any of it. Every finding below is sorted into **built / wiring gap / real gap**
with `file:line`; a default-off toggle, a null delegate, or a tool that exists but is never invoked for
13 of 16 inputs is a **wiring gap**, never reported as an architectural wall.

**Tunables are config, never code.** Any balance-surface number this program's output carries — a
tier's flat/increased amount, a power band's numeric range — belongs in `data/tuning/` or a seed file,
never a bare literal in a generator script.

**The vocabulary this program authors *into* is closed by design**, and this doc treats that as load-
bearing throughout: 9 attach points, 18 kinds, 13 triggers (`AtomKindRegistry`, verified 2026-09-07 —
DESIGN-GATE.md flags this exact count as having gone stale four times, so it was re-verified this
session rather than trusted). This program never proposes a 19th kind or a 10th attach point; it
authors *within* the 18 kinds that already exist.

## 1. What was asked

"The atom-family generator" — closing the content gap where unique items and the action corpus name
atom families that don't exist as real, playable content yet. Originally framed (in an earlier
session's memory) as "144 unique anchors name 68 atom families, only 28 real exist, only 4/144 uniques
buildable." **That framing is now stale and the real gap has moved** — re-investigated from scratch
this session, described in full below.

## 1a. DESIGN-GATE §1 rows this program touches, closed by adversarial audit

An adversarial re-read of this doc and both its module specs (2026-09-08) found that several §1 rows
whose topic clearly overlaps this program had never actually been opened, only recalled or inferred.
Closed here rather than left as a silent gap:

- **`effect-atom-map.md`** — read in full. Confirms the vocabulary counts §2 above states (9 attach
  points, 18 kinds) and names `atom-catalog-ssot.md` §2 as the per-kind detail table this doc already
  cites — no conflict, direct confirmation.
- **`effect-pipeline-ideal.md` §5, `effect-pipeline-map.md`, `effect-atom/spec-container-schema.md`** —
  read `effect-pipeline-ideal.md` §5 directly; this is the source of the L0-L4 collision §3 now names
  and fixes (`Stage-Define`/`Stage-Expand` replacing the earlier "L1"/"L2"). No other conflict — this
  program's two stages run entirely upstream of that pipeline's L0-L4 model and never touch its
  container/channel-pool/resolve machinery.
- **`action-ideal.md` §1.3, `action/spec-action-seeding.md` §3** — read directly. §1.3's "no second
  curve" rule is about action power never getting a private per-action rung/level curve outside the
  shared `rung(n)` system — unrelated to atom families, no conflict. §3's "inventing a third vocabulary
  is the exact defect the atom program exists to stop" is about demon TYPES not needing a new taxonomy
  beyond the closed action-category/tag/kind/target-mode set — also unrelated; this program consumes an
  *existing* field (`atomFamilies[]`) the action corpus already has, never adds a new vocabulary to it.
  No conflict found in either doc.
- **`docs/architecture/power/ssot-power-scale.md` §11 (caps register)** — checked; no existing register
  row names any of the 5 channels `battle-ruleset-curve-extension` will add. Relevant precedent found
  instead in `docs/architecture/decisions.md`'s "Power scale"/"Power dial"/"Caps"/"Magic numbers"
  project-wide rows: every prior change to `power-scale.v2.json` in this repo's history was paired with
  an explicit `RulesetVersion` bump-or-not verdict recorded there. `spec-battle-ruleset-curve-extension.md`
  did not originally name this — corrected in that spec directly (§7).
- **`docs/architecture/tunables-ssot.md`** — the T5 rule ("a missing tunable is a load rejection, never
  a default") is cited correctly in `spec-battle-ruleset-curve-extension.md` §3, now with its real
  location (`tunables-ssot.md` §3, T5) rather than a bare filename.
- **`decisions.md`, `software-architecture.md`** — both read. No existing locked decision names
  `tier-bands.v1.json`, `channelWeightPermille`, `powerBand`, or `FamilyExpansion` (grepped directly,
  zero matches) — no conflict, but see the `RulesetVersion` point above, which this program's own
  module 2 now accounts for.

## 2. The real, current shape of an atom and an atom family

`docs/architecture/effect-atom/definitions.md` **wins over any spec** (its own status line, definitions.md:3)
and is restated here rather than linked, per this doc's own convention:

*"Items have no behaviour. Actors do."* An atom lives on an actor's effect list; an item/trait/skill is
a **source** (bookkeeping for how the atom arrived), never a runtime participant (definitions.md §0,
lines 9-30).

**Grammar (definitions.md §1, lines 33-51):**

| Id | Pattern | Meaning |
|---|---|---|
| `family_id` | `^atom\.[a-z0-9]+(-[a-z0-9]+)*$` | one affix concept |
| `variant` | kebab-case or `''` (never NULL) | the discriminator within a family — usually an element |
| `tier` | `t1`..`t5`+ | the strength ladder |
| `atom_id` | **derived, not authored**: `{family_id}[.{variant}].t{tier}` | a row whose id doesn't match this computation is rejected (`IdMismatch`) |

**An atom family is a group of atoms sharing one `family_id`**, differing only by `tier` and `variant`
— one mechanical concept (e.g. "elemental power") instantiated across a strength ladder and, often, a
fixed axis like the 6 elements + omni. "Richness comes from families × tiers × containers, never from
growing the kind list" (`atom-catalog-ssot.md:5`).

**The vocabulary, re-verified 2026-09-07** (not trusted from a stale doc row): **9 attach points**
(`stat`, `resource`, `status`, `shield`, `board`, `match`, `ui`, `siege`, `element` — the last added
2026-09-07 by `spec-element-conversion.md` D56), **18 kinds** (`AtomKindRegistry`, `src/FusionRpg.Core/Effects/Atoms/AtomKind.cs`
— includes `stat.modify`, `stat.derived`, `resource.delta`, `resource.economy`, `status.apply`,
`status.clear`, `shield.grant`, `spawn.entity`, `board.action`, `grid.spawn`, `grid.clear`, `box.set`,
`match.modify`, `wave.control`, `bullet.modify`, `ui.present`, `structure.place`, `element.convert`),
**13 triggers** (`AtomTriggers.All`, same file — `OnSpawn`, `OnDamageDealt`, `OnDamageTaken`, `OnDeath`,
`OnGranted`/`OnRemoved` runtime-only, `OnTimer`, `OnActivate`, `OnWave`, `OnMatchStart`, `OnMatchEnd`,
`OnSunCollect`, `OnGridPlace`).

## 3. Built — a two-stage content model, and a generator already exists for the first stage

There are **two distinct authoring stages**, and confusing them is the single most important thing to
get right here — the earlier "68 named, 28 real" framing conflated them. **Named `Stage-Define`/
`Stage-Expand` rather than "L1"/"L2"** — an earlier draft of this doc used "L1"/"L2" for these, which
directly collides with `effect-pipeline-ideal.md` §5's own, pre-existing, DESIGN-GATE-required L0-L4
resolution-model vocabulary (L1 = container shape, L2 = the channel pool, L3 = value range, L4 =
resolve — an entirely different, already-shipped set of concepts). The two stages below run **before**
that pipeline ever sees a row at all — they produce the atom content the L0-L4 pipeline later consumes
as raw material — so reusing its numbering here would have been exactly the "second vocabulary for the
same-sounding concept" DESIGN-GATE's own incident log warns about. Renamed to avoid that collision
entirely, not just to relabel it.

| Stage | Where it lives | Shape | Who authors it |
|---|---|---|---|
| **Stage-Define — family definition** | `data/seed/items/affix-families/*.json` (16 `g-*.json` group files) | id, `kindId`, `params.channel`+`op` (no numbers), `powerBand` (closed enum), `frames`, `roles`, `nameWords`, `displayTemplate` | **`tools/seedsmith/seedsmith/adapters/items/affixfamgen/`** — BUILT, working, LLM-propose + deterministic-validate |
| **Stage-Expand — expanded atom rows** | `data/seed/atoms/generated/family-expand.g-*.json` | real numbers: one row per `(family, tier)`, `params.amount.{min,max,roll}` | **`FamilyExpansion.cs`** (`src/FusionRpg.Core/Effects/Atoms/Generation/FamilyExpansion.cs`) — BUILT, deterministic, but only ever invoked by hand |

**Stage-Define is functionally done.** 112 named family definitions exist today (grown from an earlier count of
17→98 over recent sessions) — every single one of the 79 distinct family ids the two real content
consumers (below) reference by name **already has a Stage-Define definition**. A generator for Stage-Define already
exists, already works, and doesn't need to be rebuilt.

**Stage-Expand is the real, current bottleneck.** Only **9 of 112** family definitions (from 3 of 16 group files
— `g-armour`, `g-attack`, `g-life`) have ever been run through `FamilyExpansion` into real, numeric,
tiered atom rows. Of the 79 families the two consumers need: **only 8/69 (items)** and **2/30
(actions)** resolve against the real expanded layer. 61 of 69 and 28 of 30 referenced families have a
name and a shape — but zero playable numbers.

**Consumers, re-counted from disk (not from a stale memory claim):**

- **Unique items**: `data/seed/items/uniques/*.json`, 18 files. Field: `fixedAtoms[].family` /
  `varianceSlot.family`. **154 anchors today, not 144** — three files carry 10 extra entries appended
  after `ssot-uniques.md:525-530` recorded the shipped count as 144 (8 roles × 18 partitions); that
  figure is real but stale by 10 rows against the actual corpus today.
  **69 distinct family ids referenced.**
- **Action corpus**: `data/seed/actions/committed-round-{1,2}.json`, 24 entries. Field: `atomFamilies[]`
  / `pairedPayoffFamily`. **30 distinct family ids referenced.**
- **Overlap**: 20 family ids are needed by both (e.g. `atom.fortitude`, `atom.vitality` — both already
  expanded; `atom.dooming`, `atom.searing-strike`, `atom.venomous`, `atom.sporing`, and 14 more — all
  still name-only). A single expansion pass serves both consumers for these 20.

**The validator that gates Stage-Expand content**, already built and already strict: `AtomRowValidator.Validate`
(`src/FusionRpg.Core/Effects/Atoms/AtomRowValidator.cs:76-212`) — id-derivation match, closed kind/op
vocabulary per kind, pooled-channel membership rules, value-spec well-formedness, trigger legality,
`power_override_json` requires a `power_note` (no magic numbers), non-blank name. This program's output
must pass this validator unmodified; it is not something this program touches or works around.

## 4. CORRECTED — the Stage-Expand mechanism is a fully-built, already-general generator; the real blocker is one tunable file's coverage, verified by actually running it

**This section replaces an earlier draft of §4/§5 that mis-scoped the problem** — the first draft
inferred the blocker from a DIFFERENT Python function (`affixfamgen.emit.resolve_tier_curve`, §5 below)
without first running the actual Stage-Expand generator to check. Running it is what `docs/DESIGN-GATE.md` means
by "test the constraint before you declare it," and doing so changed the scope substantially.

**`tools/FamilyExpandGen` already exists, is already general, and already sweeps every family file** —
not 3 of 16. `dotnet run --project tools/FamilyExpandGen -- --check`, run for real this session against
the current 16-file, 112-family corpus:

```
112 families read, 45 row(s) emitted across 3 family file(s), 103 family(ies) refused:
```

Of the 103 refusals: **98 fail with the identical reason "no authored sharePermille for family X
(channel stem X not in tier-bands.v1.json)."** Only **5** fail for a different reason —
"no referenceBaseGameUnits for channel X (op Flat) — no BattleRuleset curve is shipped for this
channel yet" — for `plating` (`arm1Max`), `quickening` (`attackInterval`), `swiftness` (`zombieSpeed`),
`flourishing` (`produceInterval`), and `carapace` (`arm2Max`). **Zero** refusals are for a
missing channel-pool match (E30's pool mechanism is not a blocker for any current family).

**The 98-family blocker is a tunable-authoring gap, not code.** `data/seed/items/_tuning/tier-bands.v1.json`'s
`channelWeightPermille` map has exactly **14 entries today — the same 14 families already expanded or
already resolvable** — and every single one of those 14 values is **`1000`** (uniform, undifferentiated).
The file's own `_meta` says as much: *"working values chosen to make the corpus resolvable, not a
validated balance decision... telemetry refits channelWeight once gameplay data exists."* Adding a
`channelWeightPermille` entry for each of the other 98 families is exactly the kind of number
`tunables-ssot.md` calls a tunable, and this repo already has a purpose-built, working CLI for
publishing exactly that: **`seedsmith numerics rebalance --set channelWeight.<id>=<value>
[--set-file <path>] --publish`** (`tools/seedsmith/seedsmith/report/cli.py:1469-1528`) — dry-run by
default, accumulates many `--set`/`--set-file` overrides in one call, writes `tier-bands.v{n+1}.json`
and leaves the prior version for revert. **This command already exists and already does exactly what
closing 98/103 refusals needs.**

**So the real, current shape of the gap is:**

1. **Wiring gap, now almost entirely closed by verification, not by new code**: `FamilyExpandGen` was
   already general; it just had never been re-run since the corpus grew from 3 to 16 group files.
   Adding the missing `channelWeightPermille` entries (a data-authoring step using the existing CLI)
   and re-running `FamilyExpandGen` (no `--check`) would resolve 98 of 103 refusals with **zero new
   code**.
2. **A genuinely open, small design question**: what value to publish for each of the 98 missing
   `channelWeightPermille` entries. A uniform `1000` (matching all 14 existing entries exactly, and
   matching the file's own stated "not a validated balance decision yet" posture) is the
   lowest-risk, most-consistent-with-precedent default. A `powerBand`-derived value (each family's Stage-Define
   definition already carries a `powerBand` enum) would differentiate low/medium/high families instead
   of treating them uniformly — a real balance choice, not a mechanical default, and one this doc
   should not make unilaterally.
3. **A separate, much smaller real gap**: the 5 refusals with no shipped `BattleRuleset` curve for
   their channel (`arm1Max`, `attackInterval`, `zombieSpeed`, `produceInterval`) genuinely need either
   a real curve added to `power-scale.v2.json`, a change to those families' authored op (Flat →
   Increased/More, which never touches a game curve at all), or an honest, permanent refusal — three
   different, mutually exclusive resolutions, a real decision for the spec phase.

## 5. Superseded — the affixfamgen numerics limitation is a different function for a different purpose

The earlier draft of this section cited `affixfamgen.emit.resolve_tier_curve`'s 14-hardcoded-name
limit (`tools/seedsmith/seedsmith/adapters/items/affixfamgen/emit.py:167-237`,
`adapters.items.channels.PRIMARY_CHANNEL_IDS`) as the program's central blocker. **That function
belongs to a different consumer entirely**: it is `affixfamgen`'s own Acceptance-#1 sanity check for
a *brand-new* Stage-Define family definition being minted (verifying its proposed tier curve is sane before the
definition is even written to disk) — not the mechanism that expands an *existing* Stage-Define definition into
Stage-Expand rows, which is `FamilyExpansion.cs`/`FamilyExpandGen` (§4), an entirely separate, independent,
already-general implementation with its own reference-base delegate
(`FlatReferenceBase` in `tools/FamilyExpandGen/Program.cs:124-130`) that is **not** restricted to 14
names — it's restricted only by which channels have a real shipped `BattleRuleset` curve, currently
`maxHp`/`hp`/`atk`/`defense`, matching §4's 5 residual refusals almost exactly. **This limitation is
real but far smaller in scope than originally described, and is not this program's central question.**

## 6. Genre prior art, with sources

**Path of Exile's tag-weight mod pool** is the closest real precedent for "a shared vocabulary of
effects, pooled and weighted per consumer, rather than bespoke per item." Mods carry tags; each base
type/consumer has a weight-per-tag; a mod with zero weight for a given consumer is silently excluded
from its pool. Leftmost tags dominate when several apply. This is structurally the same shape as this
program's family-definition `roles`/`frames`/`powerBand` fields gating which containers may draw a
family. [Modifiers — Path of Exile Wiki](https://pathofexile.fandom.com/wiki/Modifiers),
[PoE 2 item modifiers guide](https://mobalytics.gg/poe-2/guides/item-modifiers).

**Diablo 4's Aspect/Codex system** is the closest precedent for "one shared power, reused across many
items, versus a bespoke one-off." A Legendary Aspect is extracted once and can be imprinted onto any
number of future items — directly analogous to one atom family being referenced by many unique anchors
and action briefs, rather than each anchor authoring its own bespoke numbers. Genuine Unique items, by
contrast, are one-of-a-kind and never extractable — the same distinction this repo already draws
between a family-backed unique and a fully bespoke one.
[Diablo 4 Legendary Aspects guide](https://www.icy-veins.com/d4/guides/legendary-aspects-codex-of-power-guide/).

**Documented LLM-content-generation failure modes**, directly relevant to whether an *LLM* step belongs
anywhere in Stage-Expand (the numeric layer) at all: constrained decoding cannot express refusal or uncertainty,
biases toward generic/safe values ("distributional collapse"), and resists returning an intentionally
empty result ("array hallucination") — schema validity alone does not catch any of these; the source
notes "structured output modes guarantee the schema, not the quality."
[Your JSON Is Valid but Your Data Is Wrong — Towards Data Science](https://towardsdatascience.com/your-json-is-valid-but-your-data-is-wrong-five-failure-modes-llm-structured-outputs-wont-catch/).
A schema-governed pipeline for executable game content (grounding → schema-governed generation →
normalization-repair → engine-aligned admission) is exactly the shape `affixfamgen` already implements
for Stage-Define (brief hands the model only identity/enum choices, never a number; `emit.py` re-validates
independently of the schema; a deterministic numerics step prices anything admitted) — this repo's own
existing Stage-Define pipeline already reflects current best practice in the literature, which is direct evidence
*for* extending the SAME pattern rather than inventing a new one for Stage-Expand.
[Game Knowledge Management System — MDPI](https://www.mdpi.com/2079-8954/14/2/175).

**The load-bearing conclusion from genre research**: no real ARPG's numeric affix-tier curve is
LLM-generated — PoE's and D4's tier scaling is deterministic, hand-tuned, or formula-driven, with an
authored *name/theme* layer sitting on top. This matches what this codebase already does (`affixfamgen`
never lets the model see a number; `FamilyExpansion`/`resolve_tier_curve` are pure deterministic code)
and argues against ever routing Stage-Expand's actual numbers through an LLM — the open work in §5 is a numerics-
engineering problem (extend `reference_base` coverage), not a content-authoring one.

## 7. Ideal shape

A program that:

1. **Does not rebuild Stage-Define.** `affixfamgen` already works; if more *named* families are ever needed
   beyond the 112 that already exist, this is the tool to run again — no new adapter required there.
2. **Does not rebuild Stage-Expand's engine.** `FamilyExpandGen`/`FamilyExpansion.cs` already work and already
   sweep every family file — no new generator, no new orchestration mechanism.
3. **Publishes the missing `channelWeightPermille` coverage** (98 families) via the existing
   `seedsmith numerics rebalance --set-file ... --publish` CLI — a data-authoring task using an
   already-built tool, not new code. The one real choice here (§4 point 2: uniform `1000` vs. a
   `powerBand`-derived value) is this program's first actual decision point.
4. **Makes an explicit, reviewed decision** on the 5 residual no-curve refusals (§4 point 3) — add a
   real curve, change the family's op, or refuse permanently and honestly. This is the program's
   second and only other real decision point.
5. **Re-runs `FamilyExpandGen` for real** (no `--check`) once the above land, and verifies the result
   against `AtomRowValidator` and the real unique/action consumers.
6. **Never lets an LLM touch a tier number**, matching both this repo's own existing Stage-Define discipline and
   the genre-wide precedent in §6 — nothing above needs one either.

## 8. Real gaps, wiring gaps, and built — summary table

| Item | Status | Citation |
|---|---|---|
| Stage-Define family-definition generator | **BUILT** | `tools/seedsmith/seedsmith/adapters/items/affixfamgen/` |
| Stage-Define named-family coverage of the 79 needed ids | **BUILT** (0 missing) | 112 definitions in `data/seed/items/affix-families/*.json` |
| Stage-Expand deterministic expansion engine, already general | **BUILT** | `src/FusionRpg.Core/Effects/Atoms/Generation/FamilyExpansion.cs`, `tools/FamilyExpandGen/Program.cs` — verified live, sweeps all 16 group files today |
| `numerics rebalance` CLI for publishing new `channelWeightPermille` entries | **BUILT** | `tools/seedsmith/seedsmith/report/cli.py:1469-1528` |
| `channelWeightPermille` coverage (98 of 112 families missing an entry) | **REAL GAP, small & mechanical** | `data/seed/items/_tuning/tier-bands.v1.json` — 14/112 present, all uniformly `1000` |
| `BattleRuleset` curve coverage for 4 Flat-op channels (5 refusals) | **REAL GAP, small** | `tools/FamilyExpandGen/Program.cs:124-130` — `arm1Max`/`attackInterval`/`zombieSpeed`/`produceInterval` have no shipped curve |
| `affixfamgen`'s own 14-name numeric sanity check | **NOT a blocker for this program** | `emit.py:167-237` — gates *minting new Stage-Define names*, unrelated to expanding existing ones |

This doc stops here, per the idea-phase's own scope. No spec, plan, or code follows from this
document — the next step is `/spec`, and per the corrected scope above, this is likely ONE small
module (publish the missing tunable coverage, decide the 5 residual refusals, re-run the generator,
verify), not a multi-module capability map.
