# Spec: synthetic-loadout-harness (A20)

Module **A20** in the [action map](../action-map.md) §12/§12.3. Depends on **A17** (built) —
"developed alongside once A17/A18a/A18b prove out" per the map's own build order; sequenced last in
this write-up because its acceptance criteria need A18f (real dispatch) and A19 (real costs/cooldowns)
to exist before "measurably different aggregate outcomes" can mean anything beyond target selection.

> **Read `action-map.md` §12** for why this reopening exists at all: *"a Phaser client would be built
> against a system that doesn't yet do anything a loadout-comparison tool could measure"* — A20 is the
> tool that measurement was always meant to run through.

## Objective

Give balance work a **clean, repeatable, code-level way** to hand `BattleEngine` two different
multi-action loadouts on the same actor and get back a real, comparable outcome distribution across
many seeds — without touching the grant-writer (still out of scope: no durable, player-owned loadout
persistence exists), the server, or the frontend.

**What "done" looks like:** a balance pass can write, in a test or a standalone tool entry point,
*"actor X with loadout A vs. actor X with loadout B, both fighting the same opponent under 500 seeds"*
and get back real numbers — win rate, average rounds, damage-per-round — that differ **because of
the loadout**, not because of an unrelated confound (initiative order, RNG stream drift, a stat the
two builds happen to differ on by accident).

**What this module does NOT do:**
- Persist a loadout anywhere. Every loadout this harness builds lives exactly as long as the
  `BattleRunState` it seeds — the same synthetic-construction discipline A18a already established for
  its own test-input pattern, extended here to be the *production* pattern for balance tooling, not
  only for unit tests.
- Add a UI. This is a code-level harness — a Phaser-facing comparison tool is explicitly a later,
  separate program (`action-map.md` §12's own "not the frontend" framing).
- Choose which loadouts are worth comparing. That is the balance designer's call, every time; this
  module only makes the comparison mechanically cheap and correct.

## Design (locked on approval)

### 1. A builder over the existing, already-real `BattleActorSetup`

`BattleActorSetup` (`Battle/BattleModels.cs:7`) already carries `EquippedActionIds` — this module adds
no new field to it. What it adds is a small, dedicated builder (`SyntheticLoadoutBuilder` or similar,
sited beside the other harness-shaped helpers, not inside `BattleEngine`/`BattleRunState` themselves)
whose whole job is: given a base actor template (species stats, level, element) and **one or more**
candidate `EquippedActionIds` lists, produce the N `BattleActorSetup` values that differ **only** in
loadout — same `MaxHp`/`Atk`/`Defense`/`ElementPrimary`/`TraitIds`/`SpecimenId` across every variant,
so a measured outcome delta is attributable to the loadout and nothing else.

**This mirrors the existing pattern this codebase already uses for isolating one variable at a time**
(the same discipline `CombatSim`-style synthetic construction already applies for `ChannelMods` in
other balance tooling) — not a new methodology, an application of one already in use.

### 2. A seeded multi-run comparator

A small runner that, given two (or more) actor-setup variants and a fixed opponent setup, executes
the SAME battle **N times per variant**, each run seeded `(baseSeed, variantIndex, runIndex)` —
never an ambient draw, matching every other seeded stream in this codebase (`initiative`, `crit`,
`target`, `essence`, `status`) — and aggregates: win rate, mean/median rounds-to-resolution, mean
damage dealt/taken per round, and (once A19 lands) mean resource spend per resource id.

**Determinism is the entire point of a comparison tool.** Two runs of the same variant with the same
`baseSeed` must produce byte-identical aggregate output — proven by hash, the same standard every
model-free module in this repo already holds itself to, applied here to a runtime harness instead of
a content generator.

**Verified, not assumed, that this is achievable without cross-run contamination**: `ICombatRng`'s
real implementation (`SeededCombatRng`, `Combat/ICombatRng.cs:9`) is an ordinary instance class, not
a static/process-global generator — so constructing a fresh instance per `(variantIndex, runIndex)`
genuinely isolates one run's draws from the next. **The one thing to get right and name explicitly**:
the comparator must construct a **new** `SeededCombatRng`/`AtomRng`/etc. per run rather than reusing
one instance across the whole variant's N runs — reusing one would make run 2's outcome depend on how
many draws run 1 happened to make, silently reintroducing the exact cross-run contamination
determinism is supposed to rule out. State this as an explicit acceptance line (#2 below), not just a
design intention.

### 3. Output shape — a plain data record, not a report format

The comparator returns a plain, serializable record (`LoadoutComparisonResult` or similar) — win
rate, mean rounds, mean damage per round, per-resource mean spend — never a rendered report, a chart,
or a file write. Whatever consumes it (a test assertion today, a CLI or dashboard later) is a
separate, later concern; this module's contract ends at "a comparable number came back."

## Golden-safety

**This module never runs through the shipped goldens' own battles.** It constructs its own
synthetic actors and opponents for comparison runs — it has no golden-mover risk by construction,
the same way `CombatSim`'s own existing synthetic-actor tooling has none today. State this
explicitly in the module's own tests (a fixture proving the harness never touches
`data/seed`-sourced golden fixtures) rather than leaving it to be assumed.

## Acceptance criteria

1. Two loadout variants built from the same base template differ **only** in `EquippedActionIds` —
   every other field byte-equal, asserted directly, not spot-checked.
2. The same `(baseSeed, variantIndex, runIndex)` tuple produces a byte-identical single-battle
   outcome across two runs.
3. Two loadouts that are mechanically identical (same actions, different ids that resolve to the
   same envelope/container) produce statistically indistinguishable aggregate results across a real
   multi-seed run — the harness's own null-hypothesis proof that it measures the loadout, not noise.
4. Two loadouts that differ in a way A18f/A19 make real (different cooldown category, different
   resource cost) produce measurably different aggregate outcomes across the same seed set — the
   actual acceptance bar this whole reopening exists to reach.
5. The harness never reads or writes `data/seed`, never touches a shipped golden fixture, and never
   persists a loadout past the comparison run.

## Testing strategy

- Unit: the builder's own "only the loadout differs" guarantee, asserted field-by-field.
- Determinism: the byte-identical-rerun proof (#2 above), the same shape every other seeded-stream
  module in this repo already uses.
- Integration: acceptance #3 and #4 together — a same-outcome control case and a different-outcome
  case, run against real `BattleEngine`/`TimelineDispatch`, not a mock.

## Boundaries

- **Never a durable loadout.** No `EffectBinding`, no save-file row, no `rpg_action_grant`. Ask first
  if a later balance-tooling need seems to want one — that is the grant-writer's own, still-deferred
  scope, not this module's to reach into.
- **Never a UI, a report format, or a file write.** A plain data record out; everything downstream is
  a separate, later concern.
- **Never a new RNG mechanism.** Every draw uses this codebase's existing named, seeded streams.
