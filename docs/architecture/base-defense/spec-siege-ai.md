# Spec: `siege-ai`

**Module 14 of 21 · level 6 · depends on `siege-positions`, `siege-cover` · [base-defense-map.md](../base-defense-map.md)**
**Status:** spec, 2026-09-04.

---

## Objective

**An opponent that plays the board, deterministically and legibly.**

Both sides can be played (the program's founding premise), so both sides can be *not* played. The AI
is what makes a siege resolvable without a human — which is what makes step 7 the standalone-first
gate: a siege that auto-resolves is provable in CI with no FE at all.

Six requirements, R1–R6, from the ideal's audit:

| | Requirement | Why |
|---|---|---|
| **R1** | Aggro tier is separate from target choice | *"who do I care about"* and *"who do I hit"* are different questions; merging them makes taunt and threat inexpressible |
| **R2** | Additive score with a risk term | Multiplicative scores are unreadable and one zero silences everything |
| **R3** | An objective fallback | With no target in reach, advance toward the objective — not stand still |
| **R4** | Frozen acting order | Decided at round start, not recomputed as actors die mid-round |
| **R5** | Deterministic | Same inputs, same decisions, forever |
| **R6** | Readable | A designer must be able to say *why* it did that |

**Plus §5.20's five-rule minimum**, added by the completeness audit — the original spec covered rule 1
only, and *"every system surveyed has all five; the most-praised ones (Pac-Man, Into the Breach) have
*only* these."*

---

## ⛔ Decision 31 — the ⛔ on cover-seeking is overridden, and the risk is recorded

§5.17 addendum 2 forbids auto-cover-seek outright, citing five Relic patches removing it. **The owner
overruled that on 2026-09-04, against the recommendation.** Recorded here in full rather than quietly
softened, because a downstream session reads this spec and not the debate:

> **The rule that was overridden:** *"Relic shipped it and then spent five patches removing it …
> 'Infantry will no longer prefer to take paths with denser cover distribution, which has often led to
> unpredictable behaviours' (1.3.0) … **Cover should be somewhere the player decides to stand, never
> somewhere the pathfinder drifts to.**"*
>
> **The counter-argument accepted:** an AI with no notion that a cell is dangerous walks into a kill
> zone every turn. Cover then becomes a mechanic the player must respect and the opponent does not,
> which reads as broken in the opposite direction.
>
> **The residual risk is the one Relic actually hit** — *"unpredictable behaviours"*. Two things
> mitigate it that Relic's real-time pathfinder did not have: §5.20 rule 1's **total order with a
> documented tie-break** (there is exactly one valid choice, never a drift), and **R6's decision
> trace**, which makes any surprising choice explainable after the fact rather than mysterious.
>
> **If playtest reports units behaving unpredictably around cover, this is the first thing to
> suspect** — and `ai.weight.risk` set to `0` is the one-row rollback.

So the risk term **keeps** its cover discount, as specced below.


---

## What already exists (verified at HEAD, 2026-09-04)

**Built.**

- `IIntentSource` (`Battle/Timeline/IntentSource.cs`) — **`BattleEngine.Resolve`'s 8th parameter**,
  optional and trailing. Confirmed at `BattleEngine.cs:172-175`.
- `StubIntentSource` — the reference implementation, and it **already reads positions**:
  `:50-51` (caster/target), `:107` (own position), `:121` (`GridDistance.Chebyshev` to candidates),
  and `:101` documents the boardless fallback to `SourceOrder`.
- `IBattleView` — the read seam, whose doc comment states it exists so that fog *"becomes a change to
  every read the AI makes, and this interface is what confines that change to one implementation
  later."*
- `UsabilityEvaluator`, `CompiledAction`, `CooldownLedger` — action legality is already solved and is
  explicitly *not* part of `IBattleView`.
- `BoardPathfinder` (`siege-pathing`) with its two occupancy views.
- `ReachMap`/`Dijkstra` — the world-scale precedent for deterministic, ordinal-tie-broken planning.

**Real gap.** No board-aware intent source. `StubIntentSource` picks nearest and does not move.

---

## The contract

### 1. A wrapper that dispatches on side — no signature change

```csharp
/// <summary>
/// One IIntentSource for a siege, dispatching on IBattleView.SideOf. A WRAPPER: BattleEngine.Resolve
/// takes exactly one intent source and gains no parameter, so a played side and an AI side are the
/// same battle rather than two.
///
/// <para>A side whose delegate is null falls through to the AI — so "the human is playing the
/// defender" and "nobody is playing" differ by one nullable field, and auto-resolve is the default
/// rather than a special mode that could drift from the played one.</para>
/// </summary>
public sealed class SiegeIntentSource : IIntentSource
{
    public IIntentSource? PlayedSide { get; init; }
    public int PlayedSideId { get; init; } = -1;
}
```

This is what makes decision (round 6) *"both sides move"* cheap: symmetry is structural.

### 2. R1 — aggro tier and target choice are two steps

```
Step 1: tier   — which BAND of candidates do I care about?   (taunt, threat, objective proximity)
Step 2: choose — within the best non-empty band, which one?  (the additive score, R2)
```

Merging them is the classic mistake: a taunt then has to be modelled as an enormous score bonus, which
either fails to dominate or dominates so hard nothing else matters. As a **tier**, a taunt is absolute
within its tier and irrelevant outside it, which is what a taunt means.

**Which tier a candidate lands in is decided by §10's signed aggression** (−2…+2), not by a
band-membership flag. The original draft used bands; the audit replaced them because **a band can only
promote, never demote**, so stealth needed a second mechanism. One signed field does both.

### 3. R2 — additive scoring with an explicit risk term

```csharp
// Additive, not multiplicative. A multiplicative score is unreadable (nobody can say which factor
// produced a number) and one zero factor silences every other consideration. Every weight is a
// tunable; there are no literals in this method.
score = w.Damage      * expectedDamageMilli
      + w.Kill        * (isKillingBlow ? 1000 : 0)
      + w.Proximity   * proximityMilli
      + w.Objective   * objectiveValueMilli
      - w.Risk        * incomingThreatMilli;       // R2's risk term, and it SUBTRACTS
```

**The risk term is what stops an AI walking a siege unit into a kill zone** to reach a marginally
better target. Without it, cover is decorative for the AI even though it works for the player — and an
AI that ignores a mechanic the player must respect reads as broken.

`incomingThreatMilli` sums enemy damage potential reaching the candidate cell, **discounted by that
cell's cover** — which is the one line that makes `siege-cover` matter to the AI.

### 4. R3 — objective fallback

No target in reach → path toward the objective (`Core` zone for the attacker; the breach for the
defender) using **`TerrainOnlyOccupancy`**.

> This is the specific reason `siege-pathing` ships two occupancy views. With `SolidOccupancy`, a unit
> boxed in by its own allies concludes the objective is unreachable and stands still — the single most
> visible AI failure in a tactical game.

No path at all → hold and defend. **Never a random move**: an AI that fidgets is worse than one that
waits, and it burns a determinism budget for nothing.

### 5. R4 — acting order is frozen at round start

Computed once, from `OrdersBySpeed` and ordinal key tie-break, and **not recomputed as actors die**.
An order that shifts mid-round means killing an enemy can *give them a turn*, which is a bug that
looks like cheating.

### 6. R5 — determinism, and the RNG is structurally unreachable

If a tie survives every scoring term, break by **ordinal actor key** — `ReachMap`'s own discipline.

**No RNG at all in the AI.** Not a seeded one — none. Every decision is a total order over integers,
so there is nothing to roll. This is what satisfies Gate B's *"every new RNG stream is structurally
unreachable when the feature is absent"* for free: absent streams cannot leak.

**No `float`.** Every score is an integer per-mille sum. A `float` score reorders candidates
differently on different runtimes, which is a replay divergence that reproduces nowhere.

### 7. R6 — readability, on the trace that already exists

Each decision emits its **top three scored candidates with their term breakdown** to `DecisionTrace`
(`Battle/Timeline/DecisionTrace.cs`, already built). A designer asking *"why did it do that"* reads a
row, not a debugger.

Gated behind the trace being non-null, exactly as `BattleTrace` already is, so it costs nothing when
off.

### 8. §5.20 rule 2 — a **named, visible** validity filter

Clash of Clans' `Favourite Target` is a player-visible filter on every defense — Air Defense: *Air*;
Mortar: *Ground*; Archer Tower: *None*. **The player can say why it did not shoot before they watch it
not shoot.**

Its documented misses are all *features* because the rule producing them is stated: the Mortar's
4-tile dead zone, its projectile lead failure against fast troops, the Inferno's ramp reset on
retarget.

```csharp
/// <summary>
/// A named, player-visible validity filter on an action's targets. Named, because §5.20's whole
/// thesis is that STATABILITY is the requirement — a filter the player cannot name produces a miss
/// they read as a bug.
/// </summary>
public sealed record TargetFilter
{
    /// <summary>Shown in the UI verbatim. Not a debug string.</summary>
    public string DisplayKey { get; init; } = "";
}
```

Surfaced on the wire alongside `siege-cover` rule 5's contribution — one legibility channel, two
consumers.

### 9. §5.20 rule 3 — a retarget trigger with a **stated** latency

> *"Instant is not required; **specified** is."* Arknights specifies a search cycle every 3 frames, and
> attack animations complete even if the target leaves range.

`ai.retargetLatencyTicks`, a tunable, default `0` (immediate). The point is not the value — it is that
the value is **authored and stated**, so a unit that keeps swinging at a target which just moved is
following a rule the player can be told.

### 10. §5.20 rule 4 — one override channel **inside** the priority order

⛔ **This replaces the original spec's aggro *bands*.** §5.20:

> Arknights' *"aggression is a **signed scalar (+2 … −2)** inside a published five-level priority
> chain, which gives **taunt, stealth and decoy one mechanism instead of three**."*

And Damian Isla's architectural rule, from *Handling Complexity in the Halo 2 AI*:

> *"only by placing the stimulus behavior **into the tree itself** can we be assured that all the
> higher-level and higher-priority behaviors have had their say."* — **A retarget hook goes inside the
> priority order, never on top of it.**

```csharp
/// <summary>
/// Signed aggression, -2..+2, applied INSIDE the tier computation rather than as a score bonus.
/// Taunt is +2, stealth is -2, a decoy is +1 on a worthless target — ONE mechanism, three effects.
///
/// <para>The original spec used aggro BANDS, which cannot express stealth (a band can only promote,
/// never demote) and needed a second mechanism for it. A signed scalar does both.</para>
///
/// <para>Bounded -2..+2 and structural: the range IS the vocabulary, not a magnitude a balance pass
/// widens. Widening it would make aggression dominate the additive score it is meant to modulate.</para>
/// </summary>
public int AggressionOf(string actorKey);   // -2 .. +2
```

**R1 still holds** — tier first, score second. Aggression shifts which *tier* a candidate lands in; the
additive score chooses within it.

### 11. §5.20 rule 5 — a replacement vocabulary, not a degraded one

> *"A unit forced into a vocabulary that does not fit its geometry is the second-largest source of
> stupid-looking behaviour."* BTD6's Mortar has no standard priorities at all — only *Set Target*; the
> Heli gets *Patrol/Pursuit*; the Spike Factory gets *Smart*.

On this board the case is **the garrisoned emplacement** (`siege-obstacles`): it cannot move, so
"advance toward the objective" (R3) is meaningless for it. It gets its own two-entry vocabulary —
*Hold fire* / *Fire at will* — rather than an objective fallback that can never fire.

**Every vocabulary still resolves to a total order** (rule 1). A replacement vocabulary is a different
*set* of rules, never a rule that can return "no preference".

### 12. ⛔ Configurability is not on the list — statability is

> *"Kingdom Rush ships **no** targeting control at all and is a genre benchmark; what it has instead is
> a rule a player can state in one sentence ('closest to the exit') plus placement as the control
> surface. **Configurability is a convenience; statability is the requirement.**"*

**So no targeting UI is specced**, and `siege-stage` must not add one without revisiting this. What is
required is that every rule above can be stated in one sentence and is shown — which rules 2, 3 and R6
already deliver.

---

## Tunables

`data/tuning/siege.v1.json`, `ai.*`. **Every weight is here; the scoring method contains no literal.**

| Key | Unit | Default | Why |
|---|---|---|---|
| `ai.weight.damage` | weight | `100` | Balance |
| `ai.weight.kill` | weight | `300` | Balance |
| `ai.weight.proximity` | weight | `50` | Balance |
| `ai.weight.objective` | weight | `80` | Balance |
| `ai.weight.risk` | weight | `120` | Balance — **the dial that decides how cautious the AI is**, and the one a balance pass will touch most |
| `ai.retargetLatencyTicks` | sim ticks | `0` | Balance — §5.20 rule 3. The value matters less than it being **stated** |
| `ai.aggression.range` | ± | `2` | **Structural** — the -2..+2 range IS the vocabulary (§5.20 rule 4). Widening it makes aggression dominate the score it modulates. Comment says so |
| `ai.maxCandidatesScored` | candidates | `32` | **Structural** per-decision work bound, not a progression ceiling. Comment must say so |

`SiegeAiPolicy` is a Policy file, so [tunables-ssot.md](../tunables-ssot.md) makes bare literals in it
a violation by definition.

## Numeric types

| Value | Type | Why |
|---|---|---|
| scores, weights, term values | **`long`** per-mille | a weighted sum of five terms, each of which can be a magnitude — the accumulator must not be the narrow one |
| `expectedDamageMilli` | **`long`** | it is damage, which `contentScale` reaches |
| band ordinals, candidate counts | `int` | structural |

**Widen before summing**, `checked`. A score overflow silently inverts a comparison, which produces an
AI that reliably picks the *worst* option — the hardest possible bug to attribute.

## Boundaries

**Always:** integer arithmetic · ordinal tie-break · frozen acting order · every weight in tuning ·
`TerrainOnlyOccupancy` for objective pathing.

**Ask first:** a seventh scoring term · changing `IBattleView` (its doc comment explains what that
costs later).

**Never:** RNG of any kind in the AI · `float` scores · recompute acting order mid-round · a
multiplicative score · a random move as a fallback · read `BattleRunState` directly instead of through
`IBattleView` · a rule that can return "no preference" (§5.20 rule 1) · an override applied **on top
of** the priority order rather than inside it (Isla) · a targeting UI (§5.20's ⛔ — statability, not
configurability) · **reroute a path toward cover** — decision 31 permits reading cover when choosing
where to stop, and nothing more.

---

## Testing

| Test | Asserts |
|---|---|
| `Same_board_same_decisions_10000_times` | **R5**, and it is the module's central claim |
| `No_rng_is_reachable_from_the_ai` | source scan over the AI namespace for `Random`/`SeededRng` — structural, not empirical |
| `No_float_in_the_scoring_path` | the same scan, for `float`/`double` |
| `Taunt_dominates_within_its_band_and_not_outside` | **R1**, both halves |
| `Risk_term_prevents_walking_into_a_kill_zone` | **R2.** Same board, `ai.weight.risk` at 0 and at default; assert different and correct |
| `Cover_reduces_perceived_risk` | the `siege-cover` link |
| `Unit_boxed_in_by_allies_still_advances` | **R3**, and the reason for two occupancy views |
| `No_path_holds_rather_than_fidgets` | |
| `Acting_order_is_frozen_across_deaths` | **R4.** Kill an actor mid-round; assert the order is unchanged |
| `Ties_break_by_ordinal_key` | |
| `Score_overflow_throws` | not a silent comparison inversion |
| `Decision_trace_names_the_top_three_with_terms` | **R6** |
| `Trace_off_costs_nothing` | no allocation when null |
| `Played_side_delegate_overrides_the_ai` | and null falls through — the symmetry |
| `Every_target_filter_has_a_display_key` | §5.20 rule 2 — no unnamed filter |
| `Retarget_latency_is_authored_and_honoured` | §5.20 rule 3 |
| `Taunt_stealth_and_decoy_use_ONE_mechanism` | §5.20 rule 4 — assert all three are signed aggression, not three code paths |
| `Stealth_demotes_and_taunt_promotes_through_the_same_field` | which bands could not express |
| `Aggression_is_applied_inside_the_tier_not_on_top_of_the_score` | Isla's rule |
| `An_emplacement_gets_a_replacement_vocabulary` | §5.20 rule 5 — never an objective fallback it cannot execute |
| `Every_vocabulary_returns_a_total_order` | including the replacement one |
| `No_targeting_ui_is_specced` | §5.20's ⛔ |
| `Risk_weight_zero_makes_the_ai_cover_blind` | **decision 31's one-row rollback**, proven to work |
| `The_ai_never_reroutes_a_path_toward_cover` | decision 31's boundary — paths come from `siege-pathing`, which never sees a cover value |
| `A_full_siege_auto_resolves_to_a_stable_outcome` | the step-7 precondition |

## Success criteria

1. Identical decisions over 10,000 runs.
2. No RNG and no `float` reachable from the AI — proven structurally, by scan.
3. R1–R6 each have a named test.
4. `SiegeAiPolicy` contains zero bare literals; `audit-magic-numbers.py` clean.
5. A full siege auto-resolves with no human input and no FE.

## Open questions

None. R1–R6 were the open questions and the audit answered them; each is now a test.
