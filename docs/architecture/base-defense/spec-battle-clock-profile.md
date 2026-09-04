# Spec: `battle-clock-profile`

**Module 1 of 21 · level 0 · no dependencies · [base-defense-map.md](../base-defense-map.md)**
**Status:** spec, 2026-09-04. Written after Gate 0's re-survey; §3 rows 1 and 3 of the map's Gate 0
table changed this module's scope, and this spec reflects the corrected reading.

---

## Objective

**Move the battle's round horizon off the global ruleset and onto the mode profile, then add the
`siege` row.**

`BattleRuleset.MaxRounds` and `BattleRuleset.RoundDurationMs` are static and global today. A siege on
a district board needs a longer horizon than a 50-round squad fight — units walk before they swing,
structures soak, and a defender who turtles is *playing correctly*. With the horizon global, giving a
siege more rounds gives **every** battle more rounds, which moves all eight battle goldens and every
expedition golden for a change that has nothing to do with them.

This is audit finding **F2**, and it is the one thing `[JsonIgnore]` cannot save: the horizon is not a
serialized field, it is a read at three points inside `Resolve`.

**Success looks like:** `classic-round` resolves byte-identically after the move, and a fourth profile
row can name its own horizon without any other profile noticing.

## Why this is first, and why it is not negotiable

Every module after this one resolves a battle. Each resolves it under whatever horizon is in force. If
the horizon moves *after* those modules exist, each of them re-blesses whatever it locked. Moving it
now costs one re-run of a suite that is already green.

---

## What already exists (verified at HEAD, 2026-09-04)

**Built.**

- `BattleModeProfile` (`src/FusionRpg.Core/Battle/Timeline/BattleModeProfile.cs`) — the record, with
  `W`, `WScope`, `DefaultCommitment`, `PassQuantum`, `WReact`, `RendezvousEnabled`, `NewEconomy`,
  `ForecastExactness`, **`OrdersBySpeed`**, **`RequiresLiveInput`**.
- **`OrdersBySpeed` and `RequiresLiveInput` already ship.** The map's §3 said this module adds them;
  B39 and T6/B21 landed them first, each with a per-row rationale in its own doc comment. *This module
  adds neither.* It **sets** them on one new row.
- `BattleModeProfileCatalog` — three rows (`classic-round`, `galaxy-sync`, `hybrid-atb`), lazily built
  and cached, magnitudes read from `data/tuning/battle.v{n}.json`'s `timeline.profiles`.
- `BattleEngine.Resolve` **reads the profile** (`BattleEngine.cs:218-220`) — `null` resolves to
  `classic-round`. The seam is live, not inert.
- `BattleTuning.ProfileOf(id)` — per-profile tuning lookup with `W`, `PassQuantum`, `WReact`,
  `MaxPoints`.

**Wiring gap.**

- Nothing. There is no inert line here; the horizon is simply on the wrong type.

**Real gap.**

- `MaxRounds` / `RoundDurationMs` on `BattleRuleset` (`BattleModels.cs:121-122`), read at
  `BattleEngine.cs:240` (`maxBattleTick`), `:251` (initial round schedule) and `:476` (round
  rescheduling). Three reads, one type, no per-profile path.
- No `siege` row.

---

## The contract

### 1. Two new fields on `BattleModeProfile`

```csharp
/// <summary>
/// The battle's absolute round horizon. Moved here from BattleRuleset (F2): a siege needs a longer
/// horizon than a squad fight, and with the value global, giving one gives all.
///
/// <para><b>A structural bound, not a progression ceiling.</b> `AGENTS.md`'s no-hard-ceilings rule
/// exempts "per-frame/runtime caps" and this is one: it bounds how long a single battle may run, not
/// how strong anything may become. Removing it reintroduces the exact infinite loop
/// `BattleEngine.cs:236` records having already hit once.</para>
/// </summary>
public int MaxRounds { get; init; }

/// <summary>How long one round is, in simulation milliseconds. Moved with <see cref="MaxRounds"/> —
/// they are only ever read together (`maxBattleTick` multiplies them), so splitting them across two
/// types would let a profile carry half a horizon.</summary>
public int RoundDurationMs { get; init; }
```

**No literal defaults.** Both are set by `Build` from tuning for every shipped row, exactly as `W` and
`PassQuantum` already are. A hand-constructed profile in a test rig that sets neither gets `0`, and
`0` is caught by validation (below) rather than silently producing a battle with no rounds.

### 2. `BattleRuleset` keeps its properties, and they become the `classic-round` source

Do **not** delete `BattleRuleset.MaxRounds` / `RoundDurationMs`. They read
`data/tuning/battle.v{n}.json`'s `ruleset` block, which is also where `classic-round`'s horizon comes
from — and `Predictor.cs:42` has its own unrelated `MaxRounds = 400` that must not be disturbed.

```csharp
// BattleModeProfileCatalog.Build
MaxRounds       = t.MaxRounds       ?? BattleRuleset.MaxRounds,
RoundDurationMs = t.RoundDurationMs ?? BattleRuleset.RoundDurationMs,
```

`BattleTuning.ProfileRow` gains `int? MaxRounds` and `int? RoundDurationMs` — **nullable, and null
means "inherit the ruleset"**. That is what makes `classic-round` byte-identical without a special
case: it names neither, so it inherits both, so it computes the same `maxBattleTick` it always did.

### 3. `BattleEngine` reads the profile, not the ruleset

Three lines change, and the ordering matters — `activeProfile` is resolved at `:218`, and all three
reads are after it.

| Line | Was | Becomes |
|---|---|---|
| `:240` | `(long)BattleRuleset.MaxRounds * BattleRuleset.RoundDurationMs` | `(long)activeProfile.MaxRounds * activeProfile.RoundDurationMs` |
| `:251` | `rounds < BattleRuleset.MaxRounds` … `Schedule(BattleRuleset.RoundDurationMs, …)` | `rounds < activeProfile.MaxRounds` … `Schedule(activeProfile.RoundDurationMs, …)` |
| `:476` | same pair | same pair |

`:460`'s `state.Shields.Tick(roundClock.Now, BattleRuleset.RoundDurationMs, …)` moves too — a shield
whose duration is measured in rounds must measure them in *this battle's* rounds or a siege's shields
expire at squad-battle pace.

**The `(long)` widen at `:240` stays exactly where it is.** `MaxRounds × RoundDurationMs` is a
magnitude product, and `CLAUDE.md`'s rule 3 is that the cast binds to the result: `(long)a * b`, never
`(long)(a * b)`. A siege horizon large enough to matter makes this real rather than theoretical — at
the current 1000 ms round, `int` overflows past ~2.1 million rounds, but the widen costs nothing and
the rule is absolute.

### 4. `MaxLoopIterations` becomes profile-derived

`BattleEngine.cs`'s `const int MaxLoopIterations = 200_000` is justified in its own comment by the
50-round horizon: *"a 1 ms period for the full 50-round horizon is 50,000 pulses"*. A siege with a
larger horizon makes that constant wrong — it would throw on a legal battle, which is worse than the
runaway it guards.

```csharp
// The belt-and-suspenders guard, scaled to THIS battle's horizon rather than to classic-round's.
// Structural (a runaway-loop guard, exempt from the no-ceilings rule per AGENTS.md), but it must
// still be proportional or it converts a long legal siege into a thrown exception.
var maxLoopIterations = checked(activeProfile.MaxRounds * BattleTuning.LoopGuardRoundMultiple);
```

`LoopGuardRoundMultiple` is a new **structural** tunable (see §Tunables). At `classic-round`'s 50
rounds it must reproduce 200,000 exactly, or this is a behaviour change wearing a refactor's clothes.

### 5. ⛔ The economy: `ActionPointsEconomy`, and the audit corrected this

**The first draft of this spec set `points: false`, and it was wrong.** Recorded rather than quietly
fixed, because the same mistake has now been made twice in this program.

`OneActionPerTurnEconomy`'s own source states what it means:

> *"Exactly one action, spent once, per `ResetForNewTurn`. The simplest economy — every classic-round
> battle in this game today."* · `Scope => TurnEconomyScope.PerActor`

**One action per turn means move OR attack, never both.** On a 24-cell district board a unit would take
twenty-four turns to cross and never swing. It contradicts two settled things:

- `action-map.md:430`, whose heading is *"Move and attack: two separate actions, **and the clock
  decides whether you get both**"* — and which the ideal's §11.4 correction **#5** records this
  session getting backwards **once already**, while citing the wrong file.
- **Decision 14**: *"build is a third peer of move and attack."* Three peers cannot share a budget of
  one.

So the siege row runs `points: true`. That forces `timeline.profiles.siege.maxPoints` to exist —
`Build` throws otherwise, with a message that says why:

```csharp
if (points && t.MaxPoints is null)
    throw new BattleTuningRejection(
        $"battle tuning: timeline.profiles.{id} runs an ActionPoints economy but carries no 'maxPoints'.");
```

A guard that makes the omission impossible to ship, rather than a convention.

**It also stops `hybrid-atb` being the only row that exercises the other half of `ITurnEconomy`** —
B12's own acceptance line wanted a genuine second consumer, and a turn-based board that budgets
move/attack/build separately is a better one than a real-time mode nobody selects.

### 6. The `siege` row — three lines, and the catalog says so

`BattleModeProfileCatalog`'s own doc comment states the acceptance contract literally:

> *"adding a fourth mode adds a row here (plus one line in `Resolve` and one in
> `ModeProfileArchitectureTests.KnownProfileIds`), never a branch anywhere else in the kernel."*

So: three lines, and a test that proves no fourth line was needed.

```csharp
public const string SiegeId = "siege";

/// <summary>The district board (base-defense-ideal.md §5.11). Turn-based like classic-round, but
/// speed-ordered, interactive, point-budgeted, and on its own horizon — a siege is walked before it
/// is fought, and a defender who holds is playing correctly rather than stalling.</summary>
public static BattleModeProfile Siege => _siege ??= Build(
    SiegeId, AdvancePolicyKind.NextEvent, WScope.PerSide, Commitment.LateBound,
    // ActionPoints, NOT one-action-per-turn: move, attack and build are three peers (decision 14)
    // and a unit that crosses ground must still be able to swing. See §5.
    points: true,
    forecast: ForecastExactness.Exact,
    // Movement precedes contact on a board, so who steps first is a decision rather than a formality.
    // classic-round pins readiness to a constant by design; a siege must not.
    ordersBySpeed: true,
    // A siege is played, not auto-resolved — except when siege-ai drives it, which supplies its own
    // IIntentSource and never dwells. The flag states the mode's intent; it does not force a human.
    requiresLiveInput: true);
```

**`WScope.PerSide`, not `Global`.** Decision: *both sides move* (owner, round 6). Under
`WScope.Global` with `W=1` the two sides interleave one actor at a time, which is `classic-round`'s
shape and not what "both sides move" means. `PerSide` is the scope `galaxy-sync` already proves
concurrent under, in the same test file.

**`ForecastExactness.Exact`, declared not computed.** `ModeProfileArchitectureTests` bans branching on
`AdvancePolicyKind` in every file including the catalog's own rows. Next-event advance makes exactness
achievable; the row still has to *say so*.

### 7. Jitter is off, and that is a `siege` fact rather than a kernel change

Audit **F6**: any initiative jitter that exists is a squad-fight texture. On a board where the player
is choosing a move, an unpredictable acting order is not tension, it is a misclick. `OrdersBySpeed =
true` with `ForecastExactness.Exact` is *already* the no-jitter statement — the forecast is a promise,
and a promise that jitters is not one. **No new field.** If a jitter knob later appears on the
profile, `siege` sets it to zero; today there is nothing to set, and adding a field to express "off"
where "off" is already the only behaviour would be a claim rather than a feature.

---

## Tunables

`data/tuning/battle.v{n}.json`. Per [tunables-ssot.md](../tunables-ssot.md).

| Key | Unit | Default | Why tunable |
|---|---|---|---|
| `timeline.profiles.siege.w` | actors concurrent per side | `2` | Balance: how much of a side moves at once is the core pacing dial |
| `timeline.profiles.siege.passQuantum` | sim ms | `1` | Balance: how long a pass costs |
| `timeline.profiles.siege.wReact` | reaction width | `0` | Off until `siege-cover` asks for it |
| `timeline.profiles.siege.maxPoints` | action points/turn | **required, unset** | Balance — **the pacing dial.** `Build` THROWS if an ActionPoints economy carries no `maxPoints`, so this row cannot be forgotten. Decision 29 keeps the value unset until a board exists to measure on |
| `siege.actionCost.move` | points | `1` | Balance — what a step costs |
| `siege.actionCost.attack` | points | `1` | Balance |
| `siege.actionCost.build` | points | `1` | Balance — decision 14's third peer, priced beside the other two |
| `timeline.profiles.siege.maxRounds` | rounds | **unset** | Balance: the horizon. Deliberately unset in this module — decision 29 keeps force-size and duration numbers unset until a real board exists to measure them on. Unset = inherit ruleset (50). |
| `timeline.profiles.siege.roundDurationMs` | sim ms | **unset** | Same. Unset = inherit ruleset. |
| `ruleset.loopGuardRoundMultiple` | iterations per round | `4000` | **Structural, not balance** — but it lives in config because `200_000 / 50 = 4000` must stay derivable rather than being a second magic constant. Documented as structural in its own comment. |

**`classic-round`, `galaxy-sync` and `hybrid-atb` gain no keys.** They inherit, which is the whole
mechanism by which they stay byte-identical.

## Numeric types

- `MaxRounds`, `RoundDurationMs`: **`int`**. Both are structural bounds on one battle's duration, not
  magnitudes `contentScale` touches — `CLAUDE.md`'s `long`-for-magnitudes rule does not reach them.
  Neither is derived from a level, so neither goes through `P(Θ)`.
- `maxBattleTick`: **`long`**, widened before the multiply, unchanged from today.
- `maxLoopIterations`: **`int`**, and `checked` — an overflow here means a profile row asked for a
  horizon the guard cannot express, which must throw rather than wrap into a tiny cap that fails
  every battle.

## Boundaries

**Always:** keep `classic-round` inheriting · widen before multiplying · re-run the eight battle
goldens and the four expedition goldens before calling this done.

**Ask first:** setting `siege.maxRounds` to a concrete value (decision 29 keeps it unset) · touching
`Predictor.cs:42`'s unrelated `MaxRounds = 400`.

**Never:** branch on `AdvancePolicyKind` outside `BattleModeProfileCatalog` — `ModeProfileArchitectureTests`
fails it, in every file, and the ban is the module's whole acceptance argument · delete
`BattleRuleset.MaxRounds` · add a fifth `Build` overload instead of a parameter.

---

## Testing

`tests/FusionRpg.Core.Tests/Battle/Timeline/`.

| Test | Asserts |
|---|---|
| `Classic_round_horizon_is_unchanged_after_the_move` | `ClassicRound.MaxRounds == BattleRuleset.MaxRounds` and same for the duration — the inheritance path, directly |
| `All_eight_battle_goldens_are_byte_identical` | the existing golden suite, re-run. **This is the gate.** |
| `Siege_row_resolves_and_is_cached` | `Resolve("siege")` returns `Siege`, and `Assert.Same` twice |
| `Unknown_profile_still_throws` | `Resolve("sieg")` throws — the loud-over-silent stance is not weakened by a fourth row |
| `Siege_inherits_the_ruleset_horizon_when_tuning_names_neither` | decision 29's unset default, asserted rather than assumed |
| `A_profile_naming_its_own_horizon_gets_it` | configure a test tuning with `maxRounds: 120`, assert `maxBattleTick` follows |
| `Loop_guard_reproduces_200k_at_fifty_rounds` | the refactor is not a behaviour change |
| `ModeProfileArchitectureTests` (existing, extended) | `KnownProfileIds` gains `"siege"`; the file-scan ban still passes with zero new exemptions |
| `Zero_horizon_is_rejected` | a hand-built profile with `MaxRounds = 0` throws at `Build`/validation, not at round 0 |

**Determinism:** no new RNG stream, no new clock read. This module is inside Gate 0's newly-scanned
`Core/Battle` tree, so the extended `WorldDeterminismGuardTests` covers it from the first commit.

## Success criteria

1. `ClassicRound`, `GalaxySync`, `HybridAtb` resolve byte-identically — all twelve goldens green,
   unblessed.
2. `BattleEngine` contains **zero** reads of `BattleRuleset.MaxRounds` or `.RoundDurationMs`.
3. `BattleModeProfileCatalog.Resolve("siege")` returns a row whose horizon is separately settable.
4. `ModeProfileArchitectureTests` passes with exactly three lines added and no new file exemption.
5. Setting `timeline.profiles.siege.maxRounds` in tuning changes only siege battles — proven by a
   test that sets it and asserts a `classic-round` battle's tick count is unmoved.

## Open questions

None. Decision 29 deliberately leaves `siege.maxRounds` unset, which is an answered question with an
"unset" answer, not an open one.
