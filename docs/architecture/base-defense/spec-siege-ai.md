# Spec: `siege-ai`

**Module 14 of 29 · level 6 · depends on `siege-positions`, `siege-cover` · [base-defense-map.md](../base-defense-map.md)**
**Status:** spec, 2026-09-04.

**See `tasks/base-defense-todo.md`'s own MAJOR FINDING (`siege-construction`'s section) for the full
account — CLOSED, same session.** `DistrictAssaultResolver.BuildAnimateSetups` now grants construction
actions to real legion members via `AdditionalHeldActions`, and a real, multi-actor `BattleEngine
.Resolve` test proves an actor genuinely builds once combat is resolved. `SiegeAiIntentSource` itself
(target selection among held actions) is a separate mechanism — **also now wired to a real production
call site, later the same session (owner-authorized un-pause of `siege-stage`'s backend piece); see
`tasks/base-defense-todo.md`'s `siege-ai` module header, "SiegeAiIntentSource itself is now the LIVE
intent source," for the full account.**

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

### 2. R1/R2 — THREE things, not two, and the audit conflated two of them

⛔ **A draft of this spec replaced aggro tiers with signed aggression and deleted the stance. They are
different axes and both are required.**

| Axis | Lives on | Values | Answers |
|---|---|---|---|
| **Stance** (§5.16 R2) | the **actor** | `Hold` / `Guard` / `Engage` — *"three values and no more"* | *How far will I leave my post?* |
| **Aggression** (§5.20 rule 4) | the **target** | signed `−2 … +2` | *How much does this thing pull?* |
| **Score** (§5.16 R3) | the pair | additive | *Which of the pulling things do I hit?* |

**The stance is what stops the worst-looking behaviour on a defence board.** §5.16 R2, ⭐-marked:

> *"a garrison abandoning the objective to chase a bait unit — HOMM3's dragon-fly trick and Clash of
> Clans' 'Giants that have flattened every defence will happily wander off to punch a builder's hut'
> are the same defect in a turn-based and a real-time game."*

Signed aggression cannot express that: a taunt would still pull a `Hold` garrison off the Core.

```
Step 1: stance — how far may I leave my post?           (Hold / Guard / Engage, on the actor)
Step 2: tier   — which candidates pull, after aggression? (signed −2..+2, on the target)
Step 3: choose — within the best non-empty tier, which?   (the additive score, R3)
```

Merging them is the classic mistake: a taunt then has to be modelled as an enormous score bonus, which
either fails to dominate or dominates so hard nothing else matters. As a **tier**, a taunt is absolute
within its tier and irrelevant outside it, which is what a taunt means.

**Which tier a candidate lands in is decided by §10's signed aggression** (−2…+2), not by a
band-membership flag. The original draft used bands; the audit replaced them because **a band can only
promote, never demote**, so stealth needed a second mechanism. One signed field does both.

### 3. R2 — additive scoring with an explicit risk term

⛔ **A draft of this spec invented weights (kill 300 > damage 100). §5.16 R3 carries XCOM's SHIPPED
weights, and they are the opposite shape** — the correction matters because the inversion produces
precisely the behaviour the research names.

> *"XCOM's shipped weights (**hit-chance dominates lethality 70 : 15** — an AI that maximises expected
> damage with no risk term reads as **suicidal**, the most-cited 'stupid AI' complaint in the whole
> survey) plus Fire Emblem's two defensive terms and its turn-count term, a soft anti-turtle timer
> that is monotonic and invisible."*

```csharp
// Additive, not multiplicative. A multiplicative score is unreadable (nobody can say which factor
// produced a number) and one zero factor silences every other consideration. Every weight is a
// tunable; there are no literals in this method.
score = w.HitChance   * hitChanceMilli          // +70  — DOMINANT. XCOM's own ordering
      + w.Objective   * objectiveClassMilli     // +50
      + w.Kill        * (isKillingBlow ? 1000 : 0)  // +15 — lethality is a FIFTH of hit-chance
      + w.LowHp       * targetMissingHpMilli    // +10
      + w.CannotCounter * (targetCanCounter ? 0 : 1000) // +10 — Fire Emblem's defensive term
      + w.Round       * currentRound            // the anti-turtle timer, below
      - w.Risk        * incomingThreatMilli;    // −N, and it SUBTRACTS
```

**The risk term is what stops an AI walking a siege unit into a kill zone.** Without it, cover is
decorative for the AI even though it works for the player.

**`w.Round × currentRound` is a soft anti-turtle timer** — Fire Emblem's, and *"monotonic and
invisible"*. It costs nothing, it is deterministic, and it means a defender who never engages faces an
opponent that grows steadily bolder rather than one that waits forever. It is the **AI-side** answer to
the same problem F8's clock solves on the wave side.

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

### 7. R6 — readability, and `Consideration.cs` is its first real caller

Each decision emits its **top three scored candidates with their term breakdown**, gated behind the
trace being non-null exactly as `BattleTrace` is, so it costs nothing when off.

**Built 2026-09-07 (session 5) — correcting this section's own citation.** This spec named the target
as `DecisionTrace` (`Battle/Timeline/DecisionTrace.cs`, "already built"). Reading that file before
wiring anything against it found it is real but is NOT the right target: `DecisionTrace`/`TracedDecision`
replay HUMAN input decisions for `(setup, seed, trace)` determinism — a fixed
`(Tick, ActorKey, ActionId, TargetKey, Source)` shape (`Source` is `Player`/`Timeout`) with no room for
a scored candidate list, a genuinely different concern from an AI's own scoring introspection. The
"gated behind the trace being non-null exactly as `BattleTrace` is" clause in this section names the
RIGHT precedent — `BattleTrace` (opt-in, `BattleEngine.Resolve(BattleSetup, ulong, BattleTrace?)`
takes null in production, every record site null-conditional, additive lines kept OUT of `Digest` so
an observability addition can never move a golden). Built against that instead:
`AiScoring.ScoreBreakdownOf(AiCandidate, int, AiTuning) -> AiScoreBreakdown` (a new 8-field record —
one long per weighted term plus `Total`, which calls `Score` directly rather than re-summing, so it
can never silently diverge from the real total a decision used), `AiScoring.TopThree` widened to
return `(ActorKey, AiScoreBreakdown)` instead of `(ActorKey, long Score)`, `AiScoring.FormatTopThree`
(formats the breakdown as one line — `BattleTrace` stays domain-agnostic, so `Siege` formats its own
text rather than handing `Timeline` a `Siege`-namespaced type and creating a dependency opposite the
existing one), and `BattleTrace.AiDecision(round, actorKey, summary)` / `.AiDecisions` (new, its own
list, kept out of `Digest`). `SiegeAiIntentSource`'s constructor gained an 8th OPTIONAL parameter
(`BattleTrace? trace = null`, following 17.8's own `retarget` precedent exactly) — omitting it costs
nothing; supplying one records one line per REAL rescore (never on a 17.8 held-target tick, since
nothing was scored that tick). 9 new tests (`SiegeAiTests.cs` ×3, `BattleTraceTests.cs` ×3,
`SiegeAiIntentSourceTests.cs` ×3), full `CORE` 12880/12920 (40 pre-existing/external failures, all
confirmed in `Items`/`PassiveTree`/`ClassSystem`/`Expeditions` — zero in `Battle`/`Siege`/`Actions`/
`World.Turn`), `DATA` goldens 12/12 unchanged (golden-neutral by construction: kept out of `Digest`,
and `SiegeAiIntentSource` had zero production callers at this point — it does now, see below, and
goldens stayed unchanged after that too), all 4 `BOUND` guards green, `NUM`/magic-numbers audits show
zero new findings in any touched file.

**And the arithmetic is already shipped and inert.** §5.16:

> *"`World/Ai/Utility/Consideration.cs` has product-of-considerations with arity compensation **and a
> `Weakest()` that hands the turn report a reason string for free.** Its own comment says 'Nothing
> calls this yet… scoring wants an economy to score against and there is not one until
> `sector-development`.' **A siege board has one** — loam, materials, field-cap slots — **so this is
> its first real caller.**"*

**Read it before writing a scorer.** `Weakest()` is R6 for free. Two cautions if it is adopted:

- It is **product-of-considerations**, and R3 above is **additive**. Use its `Weakest()` reason-string
  machinery without importing the product form, or state explicitly why the product is right here.
- Confirm it is still uncalled. If something now calls it, follow that code.

### 7b. ⛔ Two things the AI must never do

**No hidden difficulty thumb.** §5.16:

> *"Total War's player-penalty / AI-bonus tables produce a metagame **about the resolver** rather than
> the game. `spec-ai-commander.md`'s assumption 3 already binds this — **'Difficulty is which policy,
> not a stat bonus'** — and it extends to the siege AI verbatim."*

**Do not put the score on `ActionTargetOrdering`.** *"That enum has two values, and the runtime
`TargetSpec` has no ordering field at all — it is authored, serialized, and dropped at compile.
Extending it costs a closed-vocabulary change plus a wire-contract change plus a golden move."* The
score lives in the intent source, where `bloodthirsty` already lives (`BasicAttack.cs:180-188`).

### 7c. The auto-versus-played dial is a tunable from line one

§5.16 names a tension this program cannot avoid and must not discover late:

> *"Playing it yourself should be **meaningfully better, never mandatory**"* — and with one kernel
> **both are set by the same dial**. *"Too far and auto is unusable (mandatory play); not far enough
> and playing is pointless — **fheroes2's maintainers hit the second one and openly debated making
> their auto-battle AI dumber.**"*

`ai.autoResolveHandicapMilli`, a tunable from the first line of code. **Not a stat bonus** — it selects
how many candidates the AI scores and how deep it looks, i.e. *which policy*, per 7b.

### 7d. What this AI deliberately omits

> *"no planner, no multi-turn plan, no inter-actor coordination, no adaptation to the player's build.
> Clash of Clans' entire strategic depth is a target-class enum, a layout, and a deployment position.
> **The board carries the depth; the AI carries the legibility.**"*

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

**Built 2026-09-07 (session 5), as `IBattleView.AggressionOf`, reading the `ai.aggression` derived-stat
channel (mechanism decided and wired later the same session — see the second "Resolved" note in Open
Questions).** All 3 real implementors read `0` today — no taunt/stealth/decoy status CONTENT exists
anywhere in the game yet to set the channel to anything else, so this is real, live, tested read+write
PLUMBING with no live CONSUMER, matching `TargetFilter`'s own already-shipped "named, tested, no live
consumer yet" shape (§8) — not an unbuilt mechanism.

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
| `ai.weight.hitChance` | weight | `70` | **XCOM's shipped value, and dominant by design** |
| `ai.weight.objective` | weight | `50` | XCOM |
| `ai.weight.kill` | weight | `15` | XCOM — a fifth of hit-chance. **Inverting this makes the AI read as suicidal** |
| `ai.weight.lowHp` | weight | `10` | XCOM |
| `ai.weight.cannotCounter` | weight | `10` | Fire Emblem's defensive term |
| `ai.weight.round` | weight | `1` | Fire Emblem's anti-turtle timer — monotonic, invisible |
| `ai.weight.risk` | weight | `120` | Balance — **how cautious the AI is**, and decision 31's rollback (`0` = cover-blind) |
| `ai.stance.default` | stance | `Guard` | Balance — `Hold`/`Guard`/`Engage`, three values and no more |
| `ai.autoResolveHandicapMilli` | per-mille | `1000` | Balance — the play-vs-auto dial (§7c). **Policy depth, never a stat bonus** |
| `ai.retargetLatencyTicks` | sim ticks | `0` | Balance — §5.20 rule 3. The value matters less than it being **stated** |
| `ai.aggression.range` | ± | `2` | **Structural** — the -2..+2 range IS the vocabulary (§5.20 rule 4). Widening it makes aggression dominate the score it modulates. Comment says so |
| `ai.maxCandidatesScored` | candidates | `32` | **Structural** per-decision work bound, not a progression ceiling. Comment must say so |
| `ai.objectiveReferenceDistanceCells` | cells | `20` | Balance — resolved 2026-09-07 (Open questions). The path length at which `objectiveClassMilli` bottoms out at 0; a district's own max board side is a reasonable starting point, not a formula |
| `ai.threatRadiusCells` | cells | `4` | Balance — resolved 2026-09-07. `incomingThreatMilli`'s v1 (cover-free) counts enemies within this Chebyshev radius of the candidate |
| `ai.referenceThreatPowerMilli` | power units | `-1` (unset) | Balance — resolved 2026-09-07. The raw summed enemy power that reads as "maximum" (1000‰) threat; `-1` = unset (the SAME sentinel `construction.refinePerTurnCap` already established, `SiegeTuning.cs:295-297`), `>= 0` a real value once a playtest sets one. `incomingThreatMilli` reads 0 (cover-blind, matching `ai.weight.risk = 0`'s own convention) while unset — the mechanism is real and tested, the number is not guessed |

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
| `A_hold_stance_garrison_does_not_chase_bait` | **§5.16 R2's ⭐ finding** — the dragon-fly trick, prevented |
| `Stance_and_aggression_are_independent_axes` | a taunt cannot pull a `Hold` actor off the objective |
| `Hit_chance_outweighs_lethality_seventy_to_fifteen` | **XCOM's ordering**, asserted — the anti-suicidal invariant |
| `The_round_term_makes_a_stalled_ai_bolder_over_time` | the anti-turtle timer |
| `No_stat_bonus_difficulty_exists` | 7b — source scan; difficulty selects policy only |
| `Score_is_not_on_ActionTargetOrdering` | 7b — no closed-vocabulary change, no golden move |
| `Taunt_dominates_within_its_tier_and_not_outside` | **R1**, both halves |
| `Risk_term_prevents_walking_into_a_kill_zone` | **R2.** Same board, `ai.weight.risk` at 0 and at default; assert different and correct |
| `Cover_reduces_perceived_risk` | the `siege-cover` link |
| `Unit_boxed_in_by_allies_still_advances` | **R3**, and the reason for two occupancy views |
| `No_path_holds_rather_than_fidgets` | |
| `Acting_order_is_frozen_across_deaths` | **R4.** Kill an actor mid-round; assert the order is unchanged |
| `Ties_break_by_ordinal_key` | |
| `Score_overflow_throws` | not a silent comparison inversion |
| `Decision_trace_names_the_top_three_with_scores` | **R6** — pure `AiScoring.TopThree` ordering |
| `ScoreBreakdown_total_always_equals_Score_for_the_same_candidate` | **R6** — the breakdown can never diverge from the real total |
| `ScoreBreakdown_names_each_individual_weighted_term` | **R6** — the full per-term breakdown itself |
| `FormatTopThree_names_every_ranked_candidate_and_its_total` | **R6** — the trace text shape |
| `A_real_rescore_records_the_top_three_with_their_breakdown` | **R6**, live-wired — `BattleTrace.AiDecision` |
| `No_trace_supplied_records_nothing_and_does_not_throw` | no allocation/behavior change when off |
| `A_held_retarget_tick_records_no_new_decision` | 17.8×R6 — holding isn't a new decision |
| `AiDecisions_are_kept_out_of_the_digest` | golden-neutral by construction |
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

### Resolved 2026-09-07 (session 5): the three scoring inputs §3/R2 named but never defined

Building `SiegeAiIntentSource`'s own scoring loop shipped three of `AiCandidate`'s seven terms real
(`HitChanceMilli`, `TargetMissingHpMilli`, `TargetCanCounter`) and left `ObjectiveClassMilli`,
`IsKillingBlow`, `IncomingThreatMilli` honestly at 0/false — §3 names the first and third by name and
weight, never by formula. Surfaced as assumptions, uncorrected, proceeding per this skill's own
protocol.

**`ObjectiveClassMilli`.** How strategically urgent a candidate target is, scored by its own proximity
to the DECIDING actor's objective (R3's `DistrictLayout.ObjectivePositionFor`) — an enemy standing
between me and my goal is more urgent to clear than one off to the side, matching R2's own worked
example ("prefer already-damaged targets") in spirit: proximity to the objective, not to the deciding
actor.

```csharp
// Chebyshev(candidatePos, myObjective), NOT a real BoardPathfinder route — IBattleView exposes only
// per-actor facts (PositionOf, DerivedOf, ...), never board geometry (GridSpec/IBoardOccupancy), and
// widening it to hand out the whole board would break that established shape for every other reader.
// An honest v1, the same posture HitChanceMilli/TargetMissingHpMilli/TargetCanCounter already commit
// to — a real, non-arbitrary preference signal, not the eventual precise-pathing version.
// referenceDistance: a NEW tunable (ai.objectiveReferenceDistanceCells in siege.v1.json), NOT the
//               board side literal — a balance dial the objective term's own steepness lives in,
//               matching every other AiTuning weight's own "no literal in this method" rule.
objectiveClassMilli = 1000 - clamp(chebyshevDistance * 1000 / referenceDistance, 0, 1000);
```

**`IsKillingBlow`.** Scoped to the Omni-fallback branch only, the SAME scope `SiegeHitChance` already
committed to (`OverlayCombatCalculator.Compute`'s typed-element/Divisive-mitigation branches are real,
separate work — replicating them here risks a second, silently-diverging copy of that formula, exactly
the bug class `stat.derived`/`bullet.modify`/`wave.control` already taught this program to guard
against). Reuses the SAME effective-defense expression `OverlayCombatCalculator.cs`'s own Omni branch
computes (penetration/absorption inside the delta, pierce-scaled), never re-derived:

```csharp
var effectiveDefenseOmni = def.Get(CombatDefenseOmni) * PierceFactor(
    atk.Get(CombatPenetrationOmni) - def.Get(CombatAbsorptionOmni), CombatPolicy.Default.PierceScale);
var expectedDamage = atk.Get(CombatPowerOmni) - effectiveDefenseOmni;
isKillingBlow = expectedDamage >= targetCurrentHp; // targetCurrentHp, not MaxHp
```

**`IncomingThreatMilli`.** The FULL model (§3's own words: "discounted by that cell's cover") needs a
mechanism that does not exist yet: `BattleActorSetup` carries no field connecting a live
`CombatantKind.Structure` actor back to its own `StructureDef.CoverRadius`/`CoverPowerMilli` — closing
that is its own task (add `StructureId` to `BattleActorSetup`, threaded from whichever setup-builder
places a structure), not a formula question, and is NOT resolved here. **v1, cover-free, named as
such**: sum every live enemy's raw `CombatPowerOmni` within a tunable radius of the CANDIDATE's own
position (an approximation of "danger converging near this fight").

**Superseded by the correction below — kept for the record, not the shipped formula.** The first pass
normalized this sum against a SECOND new absolute tunable (`ai.referenceThreatPowerMilli`, shipped
`-1`/unset "until a real playtest sets one"). That reference constant was itself a real defect, not a
genuine playtest-data gap — see the correction below for why and what shipped instead.

### Resolved 2026-09-07 (session 5): 17.11's WardLevel reshape — an edge-aware `ZoneOf` extension

A first attempt this session guessed the mechanism was a placement-band filter (narrowing WHERE
attackers spawn within Approach) — built, then caught as wrong by its own test (`wardLevel` WIDENED the
qualifying band instead of narrowing it) and reverted. The real design, confirmed against
§5 tunables' own `approachDepth`/`approachDepthPerWardLevel` sitting immediately after
`rampartThickness`/`fortressRampartBonus` (whose contract is `+fortressRampartBonus` ADDITIVE to wall
THICKNESS): `ZoneOf` needs the SAME additive-to-boundary treatment, applied only on the attacker's own
entry-edge side.

```csharp
// New OPTIONAL overload — every existing call site (ConstructionPlacement.CanPlace,
// ObjectivePositionFor's own internal formula, OpenCellsInZone) keeps calling the 4-parameter
// original, byte-identical, forever. Only a caller that HAS an attacker edge (DistrictAssaultResolver)
// uses the 6-parameter form.
public static DistrictZone ZoneOf(GridPos p, int side, int coreSideMilli, int rampartThickness,
    BoardEdge? attackerEdge = null, int wardExtraDepth = 0)
{
    // ... existing chebyshev/coreHalfCeil computation, unchanged ...
    var effectiveRampart = rampartThickness;
    if (attackerEdge is { } edge && wardExtraDepth > 0 && InWedgeFacing(p, center, edge))
        effectiveRampart = checked(rampartThickness + wardExtraDepth);
    if (chebyshev < coreHalfCeil) return DistrictZone.Core;
    if (chebyshev < coreHalfCeil + effectiveRampart) return DistrictZone.Rampart;
    return DistrictZone.Approach;
}
```

`InWedgeFacing(p, center, edge)` is the one genuinely new piece: for `North`, the cell's row is on the
north side of centre AND the row-distance from centre is >= the column-distance (so a cell exactly on
the NE/NW diagonal belongs to whichever wedge's own edge it is closer to, never double-counted or
gapped) — the same tie-break discipline `BoardPathfinder`'s own fixed neighbour order already
establishes for "no two equally-plausible answers." The OTHER three edges' own wedges are computed the
same way, rotated.

### Resolved 2026-09-07 (session 5): 15.6 is not an open decision — decision 5 already answers it

Re-reading `spec-siege-construction.md` §10 directly: decision 5 already states pre-battle and
in-battle deployment are "one code path, two entry points." The real gap is engineering, not
architecture: `BuildResolver.cs` (world-map `WorldSlot`/sector coordinates) and
`ConstructionPlacement.CanPlace` (tactical `GridPos`/`BoardState`) are two coordinate systems that have
never been reconciled under one shared validator. See `spec-siege-construction.md` §10's own amendment
for the resolution.

### Resolved 2026-09-07 (session 5): `IncomingThreatMilli`'s reference was a real defect — self-relative, not an absolute tunable

The stop-hook rejected "blocked, needs real playtest data" and demanded re-investigation. Re-examined
with fresh skepticism rather than repeating the same conclusion, and found the ORIGINAL v1 design
(above) was itself wrong, not genuinely blocked: it divided the summed nearby threat power by an
ABSOLUTE `ai.referenceThreatPowerMilli` milli constant. `CombatPowerOmni` scales with `P(Theta)` (the
program's own power ladder, quadratic) — a FIXED reference can never have a safe default, because "a
lot of power" at low `Theta` is trivial at high `Theta`. Shipping ANY concrete number would have been
exactly the "private per-subsystem curve" AGENTS.md's power-ladder rule forbids, and shipping `-1`
forever would leave the term permanently inert — neither is a real answer.

**The fix was already the established precedent, just not followed the first time.**
`OverlayCombatCalculator` — the real, shipped combat formula every other Omni comparison in this module
reuses (`SiegeHitChance`, `SiegeExpectedDamage`) — never compares a stat to an absolute constant, only
to ANOTHER read stat: `atk.Power − effectiveDefense`, `accuracy − dodge`. `IncomingThreatMilli` should
have followed the same shape from the start:

```csharp
// Self-relative, not absolute: nearby enemy power AS A FRACTION OF THE DECIDING ACTOR'S OWN power --
// Theta-invariant, live from day one, no tunable, no unset gate. Falls back to 0 (uncomputable) only
// when the deciding actor itself has non-positive CombatPowerOmni, guarding the division.
var selfPower = selfDerived.Get(CombatPowerOmni);
incomingThreatMilli = selfPower <= 0 ? 0 : clamp(threatMilli * 1000 / selfPower, 0, 1000);
```

`ai.referenceThreatPowerMilli` is REMOVED from `AiTuning`/`siege.v1.json`/all three test bootstraps —
not left as dead config. `ai.threatRadiusCells` (4, geometric, `Theta`-independent like
`objectiveReferenceDistanceCells`) is the only tunable this term needs. Proven Theta-invariant directly:
`The_same_reinforcement_reads_as_more_dangerous_to_a_weaker_self` holds the board fixed and varies only
the deciding actor's own power, showing the identical nearby reinforcement flips the decision in
opposite directions depending on the actor's own scale — the property an absolute constant could never
have delivered. `A_self_with_no_combat_power_reads_zero_threat_rather_than_dividing_by_zero` covers the
guard. `siege-ai`'s remaining scope narrowed to `BaseTier`/`Aggression` at this point in the session —
see the correction below for why `BaseTier` was never actually a gap, and what `Aggression`'s own
wiring turned out to need.

### Resolved 2026-09-07 (session 5): R6's own `DecisionTrace` citation was wrong — the real target is `BattleTrace`

§7's original text named `Battle/Timeline/DecisionTrace.cs` as the wiring target, "already built."
Reading that file in full before wiring anything against it found the citation itself was wrong:
`DecisionTrace`/`TracedDecision` is a real, shipped class, but it replays HUMAN input decisions for
`(setup, seed, trace)` determinism — a fixed `(Tick, ActorKey, ActionId, TargetKey, Source)` shape
with no room for a scored candidate list, a genuinely different concern from an AI's own scoring
introspection, not the same trace wearing two hats. §7's OWN gating clause ("exactly as `BattleTrace`
is") already named the right precedent without the author noticing the file-name citation two sentences
earlier contradicted it. Built against `BattleTrace` instead — see §7's own updated text above for the
full account. Same lesson as 15.6 and 17.11 this same session: read the cited file before wiring
against it, even when the spec says "already built."

### Resolved 2026-09-07 (session 5): `BaseTier` was never a gap; `Aggression`'s own named accessor was

The stop-hook rejected "`BaseTier`/`Aggression`, both blocked on content that doesn't exist" and
demanded re-investigation rather than accepting the repeated conclusion. Re-reading §2's own axis table
directly: signed aggression is named as the ONLY per-candidate input into tier computation — there is no
independent "starting tier" concept anywhere in the spec's own text. `BaseTier: 0` in `AiCandidate` is
therefore a correct STRUCTURAL constant (the anchor aggression offsets from), not a placeholder waiting
on content. Nothing to build, nothing to defer.

`Aggression` was different: §10's own snippet already named a concrete, un-built accessor —
`AggressionOf(actorKey) -> int` — that this program had simply never added to `IBattleView`. Built it
(non-nullable: unlike every other member here, `0`/neutral is always a correct answer, matching
`Stance`'s own default-value shape rather than the might-be-hidden absence convention `PositionOf`/
`MaxHpOf` use), implemented on all 3 real implementors, fog-gated on `FoggedBattleView` like `MaxHpOf`
(a target's own aggression is board information, not self-knowledge), and wired into
`SiegeAiIntentSource.ChooseTarget` in place of the hardcoded `Aggression: 0`. Two new tests prove the
mechanism actually respects R1's own "tier first, score second" rule, not just that it compiles:
`A_taunting_target_is_chosen_over_a_target_that_scores_higher` (a candidate that loses on every additive
term still wins once aggression pulls it into a strictly better tier) and
`A_stealthed_target_is_never_chosen_even_though_it_would_score_highest` (the inverse).

**Honest limit, matching §8's own `TargetFilter` precedent ("named vocabulary, no live consumer yet")**:
every real implementor still hardcodes `AggressionOf => 0` — no mechanism exists anywhere for a status
EFFECT to SET a non-default value (a real, separate design question this pass does not answer: a stat
channel, an effect payload, or a dedicated per-actor field), and no taunt/stealth/decoy content is
authored either. That write-side pair — not a formula, not a missing accessor — is `siege-ai`'s one
remaining real gap, correctly scoped as future content-authoring work rather than something to guess at
here.

**One layer deeper, checked rather than assumed**: `EntityFacts.StatusMask` — the game's own general
"bitmask of active statuses, compiler-interned status ids to bits" (`FactReader.cs`'s own doc comment) —
looked like a plausible existing seam a future taunt/stealth status could read through. Checked
`BattleRunState.FactsOf` (the siege board's own real implementation) directly rather than assuming: it
hardcodes `StatusMask: 0` for every actor. **Siege actors do not track ANY status effect today, not
specifically taunt/stealth** — a real, separate, and likely substantial gap (wiring general status
tracking into the siege actor model is its own undertaking, not a narrow fix), found but deliberately
NOT attempted here: nothing in the current base-defense plan demands general status support today, its
only relevant consumer would be the SAME not-yet-planned taunt/stealth content, and building it
speculatively ahead of a concrete need is exactly the "ad-hoc work" this program's own standing
discipline avoids. Named for whoever plans taunt/stealth content next, not solved here.

### Resolved 2026-09-07 (session 5, later still): the write-side MECHANISM — decided and wired, not left open

The previous section left "a stat channel, an effect payload, or a dedicated per-actor field" as an
undecided design question. Routed through the same investigation discipline this whole session used
rather than picked by feel: a dedicated `Explore` survey of all three candidates, each required to cite
`file:line` evidence, not an impression.

**Effect-payload** — `status.apply` (FA2) carries no magnitude at all, only `{status, duration, level}`;
the closest boolean-flag analog, `EntityFacts.StatusMask`/`FactReader.HasStatusBit`, is the wrong shape
for a signed scalar and is hardcoded off for siege regardless (previous section). **Dedicated field** —
`EntityFacts.IsMindControlled` mirrors the LIVE GAME's own native injector flag, not this repo's status
system, and is also hardcoded off for siege; `Stance` (cited above as `AggressionOf`'s own
"non-nullable default" precedent) turned out to be set from STATIC TUNING (`AiTuning.StanceDefault`,
`siege.v1.json`), never written by anything live — it sets no write-side precedent at all, only the
non-nullable-int convention. **Derived-stat channel** — the only one of the three with a real, shipped
example of the exact needed shape ("a status modifies a channel, something reads it back into
AI-adjacent math"): Nerve's `combat.accuracy.omni`/`combat.dodge.omni` debuffs
(`data/seed/dungeon/_containers/nerve.v1.json`) are read back by `SiegeHitChance.cs`, itself an
`AiScoring.Score` term.

**Decided: derived-stat channel.** Built `DerivedStatChannels.AiAggression` (`"ai.aggression"`),
registered `FlatSum`/`StatClass.Pool`/`UnitClass.GameUnits` in `DerivedStatRegistry` — deliberately no
`Cap`: `DerivedComposer.cs`'s `Cap` only clamps the top end (`Math.Min`), the wrong shape for the
symmetric `[-AggressionRange, +AggressionRange]` bound this vocabulary needs (currently ±2,
`ai.aggression.range`). That bound stays enforced exactly where it already was —
`SiegeAi.EffectiveTier` throws if `Aggression` is ever out of range — matching this repo's own "throw,
never silently clamp" rule generalized from magnitude overflow to a closed vocabulary, rather than
adding a second, competing enforcement point.

`BattleRunState.AggressionOf` rewritten from the hardcoded `=> 0` to
`(int)Math.Round(ByKey[actorKey].Derived.Get(DerivedStatChannels.AiAggression))` — a second `Explore`
pass traced `SiegeHitChance`'s own read chain (`_view.DerivedOf(actorKey)` → `BattleRunState.DerivedOf`
→ `ByKey[actorKey].Derived`, an `ActorDerivedSnapshot` computed once at actor construction and kept
live) and confirmed `AggressionOf` could reuse the EXACT SAME path, sitting in the identical class right
beside `DerivedOf` — zero new plumbing beyond the one channel registration. Byte-for-byte safe
(`ActorDerivedSnapshot.Get` defaults an untouched channel to 0, so every real actor still reads exactly
0 today, identical to the prior hardcoded return) and genuinely live (a channel mod composes
additively), both proven directly rather than assumed: `Aggression_channel_defaults_to_zero_with_no_content`,
`Aggression_channel_composes_additively_from_channel_mods` (`BattleStatComposerTests.cs`, mirroring
`Channel_mods_overlay_additively`'s own established precedent). The registry's own exhaustive
channel-count census (`StatTaxonomyTests`, `SeedCatalogTests`, `ElementHubDocDriftTests`,
`AtomCatalogSsotDriftTests` — four independent 268-hardcoding places, all updated to 269) and
`data/seed/derived-stats/catalog.json`'s own machine-readable mirror (a new `ai.aggression` row, matching
`loadout.slots`'s schema) were extended too — this program's own repeated "a new item must join every
parallel list or it silently breaks" lesson, applied to itself.

**A further correction, one layer deeper still**: the paragraph above (an earlier draft) framed the
remainder as "siege actors carry no live status-tracking at all — a substantial foundational feature."
That framing was itself too broad. A GENERIC, already-built, already production-wired mid-battle
channel-mutation mechanism exists: `BattleEffectHost.AddDerivedContribution` forwards directly to
`BattleDerivedModifierLedger.Add` (`BattleRunState.cs:353`), and `BattleRunState
.RecomposeDerivedForAllActors()` — which recomposes `Derived` from `BaseDerived` + this ledger — runs
once per actor at the START of EVERY ROUND from `BattleEngine.cs`'s own SHARED round loop
(`BattleEngine.cs:454-461`), confirmed NOT gated behind `TimelineDispatch`, so it is live for `Siege`/
`Delve` too. `AddDerivedContribution` is reachable from `DistrictAssaultResolver`'s own EXISTING
`onEffectHostReady` hook with zero new plumbing. **Proven, not just traced**:
`Host_AddDerivedContribution_reaches_Derived_and_swings_a_real_battle` (`BattleStatComposerTests.cs`)
calls it mid-construction through a real `BattleEngine.Resolve` call and shows it measurably swings real
combat output across a 20-seed sweep, the same proof `Dodge_mod_swings_fixed_battles` already
established for the setup-time path.

**What's honestly still missing, now genuinely narrower still**: only taunt/stealth/decoy CONTENT — some
effect/aura needs to be authored that calls this already-proven-live mechanism. The second item this
section used to name — `SiegeAiIntentSource` not being the live intent source in any real siege — is
CLOSED, later still the same session: see the "Resolved" section below and `tasks/base-defense-todo.md`'s
own `siege-ai` module header for the full account (owner-authorized un-pause, scoped narrowly to the
backend AI wiring, not the Phaser FE `siege-stage` itself owns). The read side, the channel, its
write-side mechanism, the scoring behavior that consumes it, AND the real production entry point that
now drives it are all real, tested, and proven end to end.

### Resolved 2026-09-07 (session 5, later still): `SiegeAiIntentSource` is now the live intent source

Full account in `tasks/base-defense-todo.md`'s `siege-ai` module header (search "SiegeAiIntentSource
itself is now the LIVE intent source") — restated briefly here since this file is the spec of record.
Owner chose, when asked directly, to un-pause `siege-stage` specifically so this mechanism gets a real
consumer; investigation found the pause was about the Phaser FE (`stages/siege/`, tasks 21.3+) — an
unrelated, presentation-layer concern — and that wiring the BACKEND intent source needs none of it.
`BattleEngine.Resolve` gained `AiTuning? aiTuning = null`; `BattleRunState` gained `DefaultAiIntentSource`
(built once per battle, using `this` as the view — valid mid-constructor, matching `Host.UseRunner`'s own
established precedent, since no external caller could otherwise ever construct one against a private
type); `DeclareBasicAttack`'s fallback chain tries it before `StubIntentSource`;
`DistrictAssaultResolver.Resolve` passes `aiTuning: SiegeTuningPolicy.Ai`. Proven via
`SiegeAiLiveWiringTests.cs`: a real siege-shaped battle where the geometrically nearest enemy is
statistically unhittable and a farther one is a guaranteed kill — the stub fallback (baseline) leaves the
far target untouched; with `aiTuning` supplied, it dies. `siege-ai`'s remainder is content-authoring only.
