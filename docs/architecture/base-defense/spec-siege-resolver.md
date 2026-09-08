# Spec: `siege-resolver`

**Module 15 of 29 · level 7 · depends on `siege-ai`, `siege-seam`, `siege-objective` · [base-defense-map.md](../base-defense-map.md)**
**Status:** spec, 2026-09-04.
**⭐ This is the gate.** When this module is green, a siege is **playable and CI-provable with no FE at
all.** Everything after it is presentation.

---

## Objective

**Join the two halves: the world asks for a siege, the battle kernel fights it on a board, the world
gets an outcome back — reproducibly, at both call sites.**

Every other module builds a piece. This one is the `IBattleResolver` implementation that puts them in
a line, and it is where the program's single most dangerous defect lives.

---

## ⛔ The defect this module exists to not have

**World turn reports are re-derived by re-simulating from turn zero**, and the re-simulation supplies
no resolver.

`RpgStore.WorldTurns.cs`, both verified at HEAD:

```csharp
:509    var result = TurnEngine.Step(world, commands, header.Seed);
:603    var result = TurnEngine.Step(world, ListWorldCommands(worldId, t), header.Seed);
```

`TurnEngine.Step`'s signature is
`Step(WorldState, IReadOnlyList<WorldCommand>, ulong, IBattleResolver? resolver = null)` — **the
resolver is optional and both call sites omit it**, so both fall back to `PlaceholderBattleResolver`.

`:603` sits inside a loop `for (var t = 0; t <= turn; t++)` that **re-runs the entire world from turn
zero** to reconstruct a report.

**So wiring only `:509` produces this:** the siege happens correctly when it is played, and then every
time the player opens the turn report, it is re-fought by the placeholder and reported differently.
The battle log disagrees with the battle. The bug appears only in the UI, only for past turns, and
looks like a display problem.

**Both call sites. Every time. This is the module's first success criterion and its first test.**

---

## What already exists (verified at HEAD, 2026-09-04)

**Built.**

- `IBattleResolver` — one method:
  `BattleOutcome Resolve(BattleRequest, IReadOnlyList<WorldEntity>, ulong seed)`.
- `PlaceholderBattleResolver.Instance` — the only shipped implementation.
- `BattleReporting.Fight` — the single funnel both existing entry points use.
- `BattleEngine.Resolve(setup, seed, trace, onEffectHostReady, profile, actionCatalog, containerResolver, intentSource)`
  — all eight parameters, verified.
- `BattleModeProfileCatalog.Siege` (`battle-clock-profile`), `SiegeIntentSource` (`siege-ai`),
  `district-layout`, `siege-seam`'s widened records.
- `TurnEngine.Step`'s optional resolver parameter — the seam is already there and already inert.

**Real gap.** Nothing implements `IBattleResolver` for a district assault, and nothing supplies one at
either call site.

---

## The contract

### 1. `DistrictAssaultResolver`

```csharp
/// <summary>
/// The world/battle join for a district assault. Deliberately thin: it TRANSLATES and DELEGATES. Any
/// combat rule that appears in this file is a rule that exists in only one of the two systems, which
/// is how a second combat model gets built by accident.
/// </summary>
public sealed class DistrictAssaultResolver : IBattleResolver
{
    readonly IBattleResolver _fallback;   // PlaceholderBattleResolver for every non-district kind

    public BattleOutcome Resolve(BattleRequest request, IReadOnlyList<WorldEntity> combatants, ulong seed)
    {
        if (request.Kind != BattleKinds.District || request.Board is null)
            return _fallback.Resolve(request, combatants, seed);   // ← every existing kind, untouched
        ...
    }
}
```

**The delegation line is the compatibility guarantee.** Sector, lane and guard battles take a path
that is byte-identical to today's, and that is provable by construction rather than by golden diff.

### 2. Six steps, in order

| # | Step | Reads |
|---|---|---|
| 1 | Build the board | `district-layout.Build(worldSeed, sectorId, slots)` |
| 2 | Build actor setups | legion members → `Animate`; slot structures → `Structure` with `MaxHp`/`BlocksMovement` |
| 3 | Place | `district-layout`'s deterministic placement, ordinal order |
| 4 | Resolve | `BattleEngine.Resolve(setup, seed, profile: Siege, intentSource: SiegeIntentSource, …)` |
| 5 | Evaluate the objective | `siege-objective.SiegeOutcomeKind` — **not "who has more survivors"**. The Core decides |
| 6 | Translate the report | survivors → `BattleSideOutcome`; structure HP → `SlotOutcome`; a side that left whole → `Withdrawn`; inconclusive → `siege-engagement`'s `Spent` |
| 7 | Return | `BattleOutcome` with `SlotResults` populated |

**Structures are built from world state and written back to it.** A slot's `StructureHp` becomes an
actor's `MaxHp` on the way in, and the actor's surviving HP becomes `SlotOutcome.StructureHp` on the
way out. `long` at every step, with no narrowing — asserted, because a single `int` cast anywhere in
that chain silently caps every structure in the game.

### 3. Seed derivation

```csharp
// The battle seed for a district assault. Mixed from the WORLD seed and the BATTLE ID, both of which
// are already deterministic (BattleKinds.IdFor is "deterministic, unique within a turn, and readable
// in a report"). Never from the turn number alone — two assaults in one turn would then share a seed
// and roll identically.
var battleSeed = SeededRng.Mix(seed, SeededRng.HashOrdinal(request.BattleId));
```

**Reuse `SeededRng`'s existing mixer.** A new hash here is a private `f(seed)` — the same defect class
as a private `f(level)`, and `district-layout` makes the same rule for the same reason.

### 4. Both call sites, and how they get a resolver

`RpgStore.WorldTurns.cs:509` and `:603` both pass one. The resolver must be **constructible without a
live world** — it takes only catalogs and tuning — so the re-derivation path at `:603` can build the
same one it used at `:509` without any stored state.

> **This is the constraint that makes the fix possible at all.** If the resolver needed live services,
> `:603` could not construct it and the re-derivation would be permanently divergent. Keep it
> constructible from statics.

### 4b. §2 rule 8 — every resolution is stamped

Found missing by pass 4. Rule 8:

> *"every resolution stamped `(engineVersion, rulesetVersion, seed)`. **A save is
> `(seed, template, command log)` and replay must be byte-identical.**"*

A district assault is a resolution, so it carries the stamp — and here the stamp is not bookkeeping,
it is what makes the `:603` re-derivation **detectable** when it diverges:

```csharp
// Without the stamp, a re-derived report that disagrees with the original looks like a UI bug.
// With it, the two carry different rulesetVersions and the cause is named in the artifact itself.
outcome with { EngineVersion = ..., RulesetVersion = BattleRuleset.Version, Seed = battleSeed }
```

**`RulesetVersion` is currently `4`** (`BattleModels.cs:108`). Adding the `siege` profile does not move
it — a new *row* is not a rules change — but **`battle-clock-profile`'s `MaxRounds` move might**, and
that is the module which must decide. Stated here because this is where a version mismatch becomes
visible.

### 5. Feature-absence is structural

Gate B: *"every new RNG stream is structurally unreachable when the feature is absent — an early
return, not a defaulted value."*

The `request.Kind != BattleKinds.District` early return at the top of `Resolve` **is** that early
return. Nothing below it can execute for a non-district battle, so no new stream is drawn and no
sequence can shift. This is stronger than a golden diff: a golden proves it did not happen once; the
early return proves it cannot.

### 6. What this module does not contain

**No combat rules.** No damage formula, no initiative, no cover math, no economy. If a number is
computed here, it belongs somewhere else. The file should read as translation.

---

## Tunables

**None.** A join module. A tunable here is a rule that escaped its own module.

## Numeric types

No new magnitudes. The obligation is **preservation**: `long` in, `long` through, `long` out, and the
tests assert it rather than the comments claiming it.

## Boundaries

**Always:** delegate every non-district kind · supply the resolver at **both** call sites · construct
from statics only · reuse `SeededRng` · `long` end to end.

**Ask first:** any combat rule in this file · a second `IBattleResolver` method.

**Never:** wire only `:509` · derive a seed from the turn alone · invent a hash · narrow a structure HP.

---

## Testing

`tests/FusionRpg.Core.Tests/World/Turn/` and `tests/FusionRpg.Data.Tests/`.

| Test | Asserts |
|---|---|
| `Re_derived_turn_report_matches_the_original` | **THE test.** Play a world with a siege, re-derive through `:603`, assert byte-identical. Fails loudly if only `:509` is wired |
| `Both_call_sites_supply_a_resolver` | a source scan of `RpgStore.WorldTurns.cs` for `TurnEngine.Step(` with no resolver argument — structural, so it cannot regress silently |
| `Resolver_is_constructible_from_statics` | no live services |
| `Non_district_kinds_delegate_unchanged` | sector, lane and guard each assert reference-equality with the placeholder's own outcome |
| `World_goldens_byte_identical` | **the gate** |
| `Same_seed_same_siege_10000_times` | full end-to-end determinism |
| `Every_resolution_carries_the_version_stamp` | **P4-3**, §2 rule 8 |
| `A_version_mismatch_between_original_and_re_derived_is_detectable` | the stamp earning its place |
| `Two_assaults_in_one_turn_get_different_seeds` | the `IdFor` mix, not the turn |
| `Structure_hp_survives_the_round_trip_as_long` | world → setup → outcome → world, no narrowing |
| `Destroyed_structures_leave_ruined_slots` | through the real `BattleApplication` path |
| `A_withdrawing_side_is_not_routed` | `siege-seam`'s F5, end-to-end |
| `A_siege_resolves_with_no_frontend` | **the standalone-first invariant, as a test** |
| `Siege_uses_the_siege_profile` | not `classic-round` |
| `No_new_rng_stream_is_drawn_for_a_non_district_battle` | the early return, asserted by comparing draw counts |

### The step-7 acceptance run

```powershell
dotnet test tests\FusionRpg.Core.Tests
dotnet test tests\FusionRpg.Data.Tests
dotnet test tests\FusionRpg.Guard.Tests
.\scripts\guard-funnel-delta.ps1
.\scripts\guard-dal.ps1
python scripts\audit-overflow.py
python scripts\audit-magic-numbers.py --summary
```

> **Run `dotnet test` with plain `>` redirection, never piped through `tail`** — a piped command
> breaks output capture and the run looks dead. Confirmed twice on this machine.

## Success criteria

1. **The resolver is supplied at both `:509` and `:603`**, and a re-derived report is byte-identical.
2. A source scan proves no `TurnEngine.Step` call omits a resolver.
3. Every non-district battle kind delegates unchanged.
4. All world goldens byte-identical, unblessed.
5. A full siege resolves deterministically over 10,000 runs.
6. No new RNG stream is reachable for a non-district battle — proven by the early return, not by a
   golden.
7. **A siege is playable and provable with no FE.**

## Open questions

None. The two-call-site requirement was the open risk; Gate 0 re-confirmed both line numbers and both
omissions.
