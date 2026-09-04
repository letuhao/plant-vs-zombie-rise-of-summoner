# Spec: `action-timing`

Module `action-timing` in the [battle-tempo map](../battle-tempo-map.md). **The program's root** — the
other four modules are unobservable until this one lands.

**Read before editing this module:** [action-ideal.md](../action-ideal.md) (sealed, 26 decisions) ·
[battle-turn-ideal.md](../battle-turn-ideal.md) §4 · [battle/audit-2026-08-21.md](../battle/audit-2026-08-21.md)
D4 · [tunables-ssot.md](../tunables-ssot.md). This spec was written after reading all four.

---

## 1. Objective

**Give every action a time cost, so the battle engine's timing model stops being a set of zeros.**

`ActionEnvelope` expresses wind-up, multi-hit resolve offsets, recovery and four cooldown classes. The
database has a column for each. **The seeder rolls none of them**, so every action in the game is
instantaneous and three of the engine's four scheduling axes measured *exactly* 0.00 % in `B34`'s sweep.

This module rolls the timing envelope — **wind-up and recovery from the action's own realized power**,
cooldown from the existing rung curve, time cost from its category — and writes it into the columns that
already exist.

### 1.1 ⛔ What this module must NOT invent — read this before designing anything

Three vocabularies already cover this ground, and `spec-action-seeding.md` §3 names inventing a fourth
as *"the exact defect the atom program exists to stop"*.

| Thing | Where it already lives | Consequence for this module |
|---|---|---|
| **Multi-hit** | **Axis B `sequence`** of the nine complexity axes — `action-ideal.md` §8.2 states it plainly: *"`resolve_offsets_json` — shipped in `ActionEnvelope.ResolveOffsets`"*. It is in `structureBudget` from **rung 7**. | Rolling multi-hit is **spending an existing budgeted axis**, not adding a feature. It must pass `StructureBudgetGuard.Check`, and a rung below 7 may not have it. |
| **Cooldown scaling by rung** | `action-rungs.v2.json` already carries **`cdMulti`** per rung (1000 → 3518). Decision **#11**: *"cooldown rides rung only. Cooldown is ticks, not a magnitude."* | Cooldown ticks read `cdMulti`. **Do not add a second cooldown curve** — that is the "one ladder" violation `ssot-power-scale.md` exists to prevent. |
| **"Reaction"** | Axis F `reaction` means **trigger-based** reactions (`OnDeath`, `actorIsKiller`). The kernel's `WReact` lane is a **different mechanism that shares the word**. | This module touches neither. `reaction-lane` is its own module and must not be conflated with axis F. |

⭐ **Wind-up itself is NOT one of the nine axes.** The axes describe *effect* structure; wind-up,
recovery and time cost are the **envelope's timing**, orthogonal to complexity. So this module adds no
axis and needs no vocabulary change — it fills declared fields.

### 1.2 Users

The player, indirectly: actions that take time are actions you can see coming. Directly, the battle
engine — `ActionSlots`, `Commitment` and `TurnState.Committed` all become observable the moment
`WindupTicks` is non-zero for anything.

---

## 2. Design

### 2.1 The token wind-up on the basic attack (owner decision 2)

The basic attack gets a non-zero wind-up. Everything else follows from that one number.

**Why a token rather than a real telegraph:** it makes `W` contend and `Commitment` observable — the two
axes measured at 0.00 % — **without re-pitching the combat floor**. Every actor shares the basic attack,
so a floor-wide change is close to a no-op in *relative* terms. Real telegraphs stay the skills' job,
where they differentiate.

✅ **Settled 2026-09-04 (owner): the token is a MEANINGFUL fraction of the round — a felt beat**, not the
minimum that technically unlocks the knobs.

⭐ **This pairs deliberately with `tempo-content`.** A felt wind-up is what turns speed ordering into
**first-strike**: if an exchange has rhythm, acting earlier means landing before a rival, and a kill
removes their turn entirely. With a minimal token, speed would decide an order nobody could perceive.
The two modules land together (D5) precisely because they are one mechanic seen from two sides.

⚠️ **Accepted cost, stated plainly:** a felt beat on the floor **every actor shares** means a larger
balance pass and more golden movement than a minimal token. That is the trade the owner took, and the
staged sweep must size it before the joint re-bless rather than after.

`BasicAttack.BasicAttackEnvelope` currently derives from `ActionEnvelope.NoOp`. It gains `WindupTicks`
and `RecoveryTicks` **read from tuning**, never a literal.

### 2.2 What gets rolled, and from what

⚠️ **Read D2 below first** — this is derived at *catalog build*, not by the seeder.

Per action. **Wind-up and recovery scale with the action's realized power** (§2.2a); the remaining
fields key off rung and the five shipped `ActionCategory` values — no new vocabulary either way:

| Field | Source | Rule |
|---|---|---|
| `windupTicks` | ⭐ **the action's own realized power** (owner decision, see §2.2a) | Payoff-scaled, **not** category-scaled. A big payoff winds up long whatever category it wears. |
| `recoveryTicks` | realized power, same scale, smaller coefficient | The cost of having acted; keeps `W` contention meaningful after resolve. |
| `timeCostTicks` | category base | Feeds readiness. Already the envelope's documented "pre-speed time quantum". Category is fine here — this is turn *rhythm*, not telegraph. |
| `cooldownTicks` | **`cdMulti` from `action-rungs.v2.json`** × category base | Decision #11. **Reads the existing curve.** |
| `cooldownClass` / `cooldownKey` | category | `Category`-class cooldown keyed on the action's own category is the default; `None` stays legal. |
| `resolveOffsets` | **axis B `sequence`**, rung ≥ 7 only | Must be declared to `StructureBudgetGuard` as spending `sequence`. Default stays the shared single-resolve `[0]`. |

### 2.2a ⭐ Payoff-scaled wind-up — and the number already exists

**Owner decision:** wind-up tracks *what the action actually does*, not which category it wears. Big
damage or big utility winds up long; a cheap effect is quick regardless of category.

### ⛔ D2 — this is derived at CATALOG BUILD, not in the seeder

**Review finding, 2026-09-04.** The first draft said *"the seeder rolls the timing envelope"*. **The
Python seeder cannot** — it does not compute realized power. `ContentValidation.Budget` is C#, and its
rung-keyed overload has a real production caller: **`RpgStore.BuildActionCatalog`**. The seeder only
mentions `powerBudgetMilli` in a gate comment.

**So the split is:** the seeder emits atoms, category, rung and targeting as it does today — **no Python
change for wind-up at all** — and `BuildActionCatalog` / `ActionCompiler` derives the timing envelope
where the power number already lives. That is simpler than the original plan and puts the derivation
next to the validation that already guards it.

**This needs no new computation.** Every action already carries a **realized power** figure, validated
at compile time against its rung's budget:

- `action-rungs.v2.json` carries **`powerBudgetMilli`** per rung — 1000 at rung 1 rising to **37 221** at
  rung 10.
- `ContentValidation.Budget` already checks a composed action's power against that rung-keyed budget.
- `ActionRejection.PowerBudgetExceeded` already exists for the failure.

So wind-up reads a number the pipeline **already computes and already enforces**:

```
windupTicks = min(windupCapTicks, windupPerPowerMilli × realizedPowerMilli / 1000)
```

### 2.2b ⛔ D1 — the bound is not optional, and an unbounded version is unplayable

**Review finding, 2026-09-04.** The first draft of this spec had no bound. Measured against the shipped
numbers, that is broken:

| | Value |
|---|---|
| `powerBudgetMilli`, rung 1 → rung 10 | 1000 → **37 221** (a **37.2×** spread) |
| `roundDurationMs` | 1000 |
| `maxRounds` | 50 |

A linear map gives a rung-10 action a **3.7 – 14.9 round** telegraph. At a 400 ms rung-1 base that is
~**30 % of the maximum battle**, and most battles end well before round 50 — so **the game's best action
would frequently never land.** An action strictly worse than doing nothing is not a balance problem, it
is a design error.

⭐ **The fix is a precedent that already exists, not a new idea.** `action-ideal.md` decision **#10**:
*"Duration rides the ladder, with a bound that is **relative, never absolute**."* Wind-up is a duration
riding the ladder, so it takes the same treatment:

- **`windupCapTicks` is expressed relative to `roundDurationMs`**, never as a raw millisecond literal —
  so the cap tracks the battle's own timescale if that is ever retuned.
- `ActionEnvelope` already reserves **`DurationMinTicks` / `DurationMaxTicks`** for exactly this shape.
- ⚠️ **The cap is a balance dial, not a structural limit** — it shapes how the game feels, so PS-8
  applies: it is a **configurable soft cap** in `data/tuning/`, never a `const`, and never a silent
  clamp that hides a mis-tuned coefficient.

✅ **Settled 2026-09-04 (owner): drive wind-up from `qPowerMilli`, not `powerBudgetMilli`.**
The budget is a *ceiling* an action may spend up to; the quantum is what a rung actually **buys**. At
rung 10 that is **12.4×** rather than 37.2× — the spread halves before the cap ever has to act.

⭐ **D10, verification round 2: the decision also removes a failure mode nobody had spotted.**
`RungRow.PowerBudgetMilli` is **`long?`, and null for any table loaded before that column existed** —
`action-rungs.v1.json` and every inline test fixture. Its own doc pins the contract: *"`null` … never
`0`, which would read as 'budgets nothing' … a caller that needs the budget **skips** a rung reporting
`null`."*

⛔ **A wind-up driven by the budget would be UNDEFINED on those rungs**, and "skip" is not an option when
every action needs a duration — the module would have had to invent a fallback curve, which is a second
curve by another name. `RungRow.QPowerMilli` is a **non-nullable `int`, present on every row ever
authored**. So decision 8 is not only better-shaped, it is the only driver that is always there.

⭐ **Why the shape matters beyond a smaller number.** If the cap does most of the shaping, **the cap becomes
the real curve** and the power relationship it was supposed to express is flattened out of existence.
Driving from the quantum keeps the curve in the ladder where it belongs and leaves the cap as what it
should be — a guard against the tail, not the mechanism.

⭐ **Why this is the right shape rather than a convenience.** It gives the scaling for free in both
directions — *across* rungs (budget grows ~37×, so high-rung actions telegraph) and *within* a rung (an
action spending its budget on a single big payoff telegraphs more than one spreading it thin) — from
**one** coefficient, with no second curve. `ssot-power-scale.md`'s "one ladder" rule is satisfied by
construction: this is a projection of the power number, not a new `f(rung)`.

⚠️ **Divide by 1000 last, exactly once**, and keep every intermediate `long` — `realizedPowerMilli` at
rung 10 is already five figures before the coefficient multiplies it (`CLAUDE.md`, numeric overflow).

⛔ **The basic attack is exempt from the formula.** It has no rung and no seeded power, so its token
wind-up (§2.1) is its own tuning value, not a derivation. Keeping it out of the formula is what stops
the token from drifting when the coefficient is tuned.

### 2.3 Every number lives in `data/tuning/action-timing.v1.json`

**No timing number appears in code.** `tunables-ssot.md`'s test — *"would a balance pass ever want to
change this?"* — is unambiguously yes for all of them: wind-up length is the single most felt number in
an action game.

The file carries the **wind-up/recovery power coefficients**, the per-category time-cost and cooldown
bases, and the basic attack's token — and is **loaded by a host and injected**, never read by Core
(`tunables-ssot.md` §7.2). A missing key is a **load rejection naming it**, never a default.

⚠️ **`long` for every tick field.** These are magnitudes the `Θ` ladder can drive
(`CLAUDE.md`: `long` for any magnitude, never `float`, divide by 1000 last, overflow throws).

### 2.4 What this module does NOT change

- **No engine change.** `BattleEngine` already reads the envelope; it simply reads zeros today.
- **No new `ActionEnvelope` field.** Every field it writes already exists.
- **No structure axis.** See §1.1.
- **No change to `Interruptible`.** Interruption is `reaction-lane`'s business.

---

## 3. Commands

```powershell
# the seeder half
python -m pytest tools/seedsmith/tests -k timing

# the consuming half
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~ActionTiming|FullyQualifiedName~ActionCompiler"
dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~Action"

# the movement this module owns
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Golden|FullyQualifiedName~Expedition"

# balance surface + guards
python scripts\audit-magic-numbers.py --summary
.\scripts\guard-single-writer.ps1
```

---

## 4. Project structure

```
data/tuning/action-timing.v1.json                      NEW — every timing number
tools/seedsmith/…                                      ⛔ NO CHANGE — see D2; the seeder cannot
                                                       compute power, so it rolls no timing
src/FusionRpg.Core/Actions/ActionTimingTuning.cs       NEW — pure parser, Core reads no file
src/FusionRpg.Core/Actions/BasicAttack.cs              token wind-up from tuning
src/FusionRpg.Core/Actions/ActionCompiler.cs           envelope assembly from the row
src/FusionRpg.Data/Sqlite/RpgStore.Actions.cs          columns EXIST — no migration
tests/FusionRpg.Core.Tests/Actions/ActionTimingTests.cs NEW
```

---

## 5. Code style

Mirror `ItemRarityTuning` exactly — it is the shipped precedent for a tuning parser:

```csharp
/// <summary>Pure parser over `data/tuning/action-timing.v1.json` — no file I/O
/// (tunables-ssot.md §7.2: "Core never reads a file. Hosts load and inject.").</summary>
public static class ActionTimingTuning
{
    public static IReadOnlyDictionary<string, ActionCategoryTiming> Parse(string json)
    {
        // a missing key is a REJECTION NAMING IT, never a default — a silent default here
        // would make an unauthored category resolve to an instantaneous action, which is
        // exactly the state this module exists to end.
        ...
    }
}
```

---

## 6. Testing strategy

1. **The envelope actually arrives.** A seeded action's rolled timing survives the round trip
   seeder → JSON → store → `ActionCompiler` → `ActionEnvelope`. Asserted on a **real committed row**,
   not a fixture — the `AuthoredEligibilityResolvesTests` lesson: synthetic rows proved the mechanism
   while the shipped content was unreachable.
2. ⭐ **The three dead axes come alive — by contrast, and this is the module's real acceptance.**
   Re-run `HybridAtbSweepTests`' staged attribution. `W` and `Commitment` must **stop** measuring
   0.00 %. A test asserting they are non-zero is the proof the program's premise was right.
3. **`classic-round` still contains `hybrid-atb`.** The containment claim of the ideal must survive:
   pinning the knobs must still reproduce round-robin.
3a. ⭐ **Wind-up correlates with payoff, both ways.** A higher-power action at the same rung winds up
   longer than a lower-power one, and rung 10 winds up longer than rung 1 — asserted against the
   **real** `qPowerMilli` values from `action-rungs.v2.json` — ⚠️ the **driver decision 8 chose**, not
   `powerBudgetMilli`, which this line named until the coverage audit caught it. Asserting against the
   rejected driver would have let a second curve creep in through the test itself.
4. **Cooldown reads the existing curve.** A rung-10 action's cooldown equals `cdMulti[10]` × its
   category base — asserted against `action-rungs.v2.json`, so a second curve cannot creep in.
5. **Multi-hit spends its axis.** A rolled `resolveOffsets` of length > 1 at rung < 7 is **refused** by
   `StructureBudgetGuard`, and at rung ≥ 7 is accepted and counted.
6. **No timing literal in code.** `audit-magic-numbers.py` stays at `M1 = 0`; a planted literal must
   redden it (falsifier).
7. **Overflow.** Tick fields are `long`; a `Θ`-driven magnitude that would overflow **throws**.

---

## 7. Boundaries

- **Always:** put every timing number in `data/tuning/`; read `cdMulti` from the shipped rung table;
  use `long` for ticks; declare multi-hit as spending axis `sequence`.
- **Ask first:** changing the *shape* of the rung curve; giving the basic attack more than a token
  wind-up; any `Interruptible` default other than `OnCC`.
- ✅ **Settled 2026-09-04 (owner):** `Interruptible` stays **`OnCC`** — only crowd control stops a
  telegraph, so a slow action remains worth building around and damage does not silently delete it.
  **This is the current default, so this module changes nothing here** — recorded so a later session
  does not re-open it as an oversight.
- **Never:** invent a structure axis (§1.1); add a second cooldown curve; hardcode a tick literal;
  hand-author an action (sealed decision #4 — actions are seeded); change `ActionEnvelope`'s fields.

---

## 8. Success criteria

1. Every seeded action carries a non-zero timing envelope — **wind-up derived from its realized
   power**, cooldown from `cdMulti` — verified on real committed rows.
2. **`W` and `Commitment` no longer measure 0.00 %** in the staged sweep — the premise, proven.
3. Cooldown ticks derive from the existing `cdMulti`, with no second curve.
4. Multi-hit exists only at rung ≥ 7 and passes the structure-budget guard.
5. `M1 = 0`; no timing literal in code.
6. The golden re-bless is **predicted before it is run**, bumped once, swept, and signed off.

---

## 9. ⚠️ Golden movement — this module owns the whole program's

Every other module in `battle-tempo` inherits byte-identity from this one landing first. Actions taking
time changes fight length, turn order and win rate — **the point, not a side effect**.

- ⭐ **D5 (owner, 2026-09-04): `action-timing` and `tempo-content` land TOGETHER as one mover.** Both
  move goldens — wind-up changes fight length, species tempo changes turn order — and
  `decisions.md`'s *Golden ordering across streams* is explicit that two separate re-bless events cost
  two sweeps and two owner sign-offs for the same goldens. One bump, one re-bless, one sweep, one
  sign-off, covering both.
- ⚠️ **The consequence: their deltas are NOT independently attributable.** That is the accepted price.
  Mitigate it the way `B34` did — measure each axis separately in a **staged sweep** before landing
  them jointly, so the attribution exists even though the re-bless does not separate it.
- **Predict the movement before running it**, per `decisions.md`'s *Golden ordering across streams*.
- ⛔ **Land it once, here.** `commitment-binding`, `reaction-lane` and `forecast-rail` must each be
  byte-identical on top of this — if a second module moves goldens, neither can attribute its delta.
