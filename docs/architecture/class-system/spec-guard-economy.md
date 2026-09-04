# Spec: `guard-economy` — `poise` as one ratio, and BASTION's missing offence

**Module id:** `guard-economy` · **Program:** [class-system-map.md](../class-system-map.md) ·
**Status: AUTHORIZED 2026-08-26 -- owner's /goal directive commands execution of the class-system plan to completion; supersedes this "awaiting owner review" header, which was never flipped after that directive landed.**

**Depends on:** `aptitude-resolve` · **`poise-resource`** — [spec-poise-resource.md](spec-poise-resource.md)

> **Corrected 2026-08-26.** An earlier header called the resource amendment *"not this module's to
> make"* and left it as an owner action. **The owner's part was the decision** (register `poise`, taken
> 2026-08-26); writing it is spec work, and it is now module 4 — which also makes this module's block a
> dependency arrow rather than a note.

---

## 1. Objective

Make the guard a **decision with a price** instead of a passive proc — by giving `poise` a cost shape, a
regeneration rate, and a conversion on release.

**Three owner decisions, taken 2026-08-26, that together make one mechanism:**

| | Decision |
|---|---|
| **Cost** (§5b.3) | **Reading C** — a small flat cost to *raise* the guard, **plus** an absorb drain ∝ what it stopped |
| **Regen** (§8.3) | **Per-tick, sized low** against peer pressure |
| **Riposte** (§8.9) | Spent `poise` **converts to damage** |

> **Together they are a single ratio, not three features.** `poise` drains proportionally to what it
> stopped, regenerates at a rate sized against incoming pressure, and converts on release. **A heavy
> attacker beats a guard by arithmetic rather than by a special case** — which is exactly what the
> FORCE → BASTION arrow is supposed to be.

**Users:** BASTION builds; the action layer's reaction lane; `balance-guard`, because a guard that
never runs out is the termination defect wearing a different hat.

---

## 2. ⛔ Blocked on an ADR, and it is a real block

`DerivedStatChannels.ResourceIds` is the **locked five** — **verified in code 2026-08-26**:
[DerivedStatChannels.cs:475](../../../src/FusionRpg.Core/Stats/Derived/DerivedStatChannels.cs) is
literally `new[] { "hp", "stamina", "hunger", "spirit", "qi" }`, not merely asserted from
[decisions.md](../decisions.md)'s *Resource model* row: *"Five actor resources, one shared set: `hp` · `stamina` · `hunger` · `spirit` · `qi`"*,
and [resource-hub-ssot.md](../resource-hub-ssot.md) §5: *"Closed set; **adding one is an ADR**."*

So there is no `resource.max` channel for **poise**, `SpecChannelClaimTests` refuses a spec that claims
it, and **none of this is testable** until the five → six amendment lands.

**Until then, guard costs `stamina`** — which is what [resource-hub-ssot.md](../resource-hub-ssot.md)
§2 already says it does (*"`stamina` pays for physical actions — move, basic attack, **guard**,
reposition"*). That is a working fallback, not a gap.

**The amendment is cheap and the hub already says so:** *"The registry is data. Adding a sixth resource
costs a row, not a system."* [spec-poise-resource.md](spec-poise-resource.md) §3 **verified that in
code** — `DerivedStatRegistry.cs:165-171` registers all three channels in a loop over `ResourceIds`, so
the edit really is one array element plus one JSON row.

**What the amendment must carry**, so it is one edit rather than three:

| Field | Value | Why |
|---|---|---|
| `class` | `body` | It is stopped by the body, alongside `stamina` |
| `polarity` | `asset` | Fills up, you spend it, empty is bad — like all five |
| `accrual` | `regen` | §4, per-tick |
| `onEmpty` | **exhaustion status** | Guard broken. Every resource except `hp` has one |
| `labels` | plant / zombie | Content, never a key |
| **Not** `hp`'s exemption | | `poise` at zero is a broken guard, not death |

---

## 3. `poise` as designed is a shield, and that is the problem reading C solves

**The two shipped rules it sat between:**

- [action/spec-action-costs.md](../action/spec-action-costs.md) §3: *"**Committing** is what costs, not
  landing. Interrupted, fizzled, and missed actions have all paid. One rule with no exceptions."*
- The same spec §7 excludes shields for a stated reason: *"nothing ever pays a shield to act."*

A guard priced purely on what it stopped **costs nothing when it stops nothing** — landing-costs, the
one shape that rule forbids. By its own definition, `poise` drained only by absorbing **is a shield**.

| Reading | What it means | Cost |
|---|---|---|
| A | `poise` moves to the damage layer, shield-like | Honest, but BASTION keeps a passive defence and still has nothing to spend on winning |
| B | Guard becomes a declared action with a flat commit cost | Obeys "committing costs", but throws away the "big hits drain you faster" pressure that made `poise` interesting |
| **C** | **Both** — flat commit **plus** absorb drain ∝ what it stopped | **Obeys both rules instead of breaking one** |

> **The flat part is the *action* (committing costs, always); the proportional part is the *mitigation*
> (output is priced).** Two different rules governing two different things, which is what each was
> written for. It also makes guard a **decision** rather than a proc.

---

## 4. Regen — per-tick, sized low, and the binary dissolves

§8.3 asked per-tick or per-encounter. **It is not a binary**, and the answer was already a rule in this
program — §5d.3: *regeneration must be sized against the damage a peer deals, never against the pool it
refills.*

With `r = poiseRegen / peerPressure`:

| `r` | What guard behaves like |
|---|---|
| `0` | **per-encounter** — a finite budget; break it and BASTION is defenceless for the rest of the fight |
| **low** | pressure outpaces regen: **heavy hits break the guard, attrition does not** ✅ |
| `≥ 1` | per-tick and unbreakable — **the same defect the termination invariant names** |

**Per-encounter is the `r = 0` corner of a continuum, not a rival to it.** A hard per-encounter budget
makes the break a coin-flip on fight length: in a long enough fight it is guaranteed, in a short enough
one it never happens.

**And it is the same dial as everything else.** `poise` joins `recovery.families` in
`aptitudes.v{n}.json` and is solved the way `resource.regen.hp` was — measured, not guessed, and
re-solved whenever damage moves. **One mechanism, not a second.**

---

## 5. The riposte — BASTION's missing offence

FORCE spends `stamina` to attack and FINESSE spends `qi` to cast. **BASTION spends `poise` to block** —
so two postures have an offence economy and one does not.

> **Spent `poise` converts to damage.** The defensive spend becomes the offensive one, so BASTION's
> single resource does both jobs rather than needing a second pool.

This is what makes reading C *necessary* rather than merely tidy: a guard that costs nothing when it
stops nothing would also **produce** nothing, and BASTION would still have no way to win.

---

## 6. Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "Poise|GuardEconomy"
dotnet test tests\FusionRpg.Core.Tests --filter SpecChannelClaim     # red until the ADR lands
python scripts\audit-magic-numbers.py --domain aptitudes
```

---

## 7. Project structure

```text
src/FusionRpg.Core/Actions/Defence/PoiseLedger.cs      the cost path: flat commit, absorb drain, per-tick hold
src/FusionRpg.Core/Actions/Defence/Riposte.cs          the riposte conversion (bounded ratio, PS-8 exempt)
src/FusionRpg.Core/Actions/Cost/ActorResourcePools.cs  the pool itself - the six-resource SSOT, not a private dictionary
src/FusionRpg.Core/Stats/Derived/DerivedStatChannels.cs  ResourceIds - `poise` is registered (the ADR landed)
data/tuning/aptitudes.v{n}.json                        recovery.families gains poise regen
tests/FusionRpg.Core.Tests/Actions/PoiseLedgerTests.cs
tests/FusionRpg.Core.Tests/Actions/PoiseTerminationTests.cs
tests/FusionRpg.Core.Tests/Actions/DefenceActionRiposteTests.cs
```

> **Amended 2026-09-05 (`battle-tempo` `poise-unification`).** This module originally shipped as
> `Combat/Guard/PoiseRuntime.cs` — a self-contained pool with its own `Dictionary<string, long>`,
> built inert (no production caller) ahead of the action layer that would trigger it. When
> `spec-defence-actions.md`'s T25/T26 later built the SAME three-part cost against
> `ActorResourcePools` (the resource SSOT `PoiseRuntime` predates), the two never merged — a real,
> named fork (`battle/audit-2026-08-21.md`-style finding **D9**), harmless only because both stacks
> had zero callers. `battle-tempo`'s `reaction-lane` module was the first caller in line, which is
> what forced the reconciliation: `PoiseRuntime.cs` and its test file are **deleted**;
> `PoiseLedger`/`Riposte`/`ActorResourcePools` are the surviving, single path. Every property this
> spec's §9 table names was migrated, not dropped — see
> [spec-poise-unification.md](../battle-tempo/spec-poise-unification.md) §6 for the mapping.
>
> **One behavioural decision fell out of the merge, and it favours this spec's OWN §3 intent over
> the code `PoiseRuntime` shipped:** its `Commit` floored at zero instead of refusing, justified in
> its own comment as a PS-8 requirement (*"a 'cannot afford to guard' refusal would be exactly [a
> hard cap] in a different shape"*). That reasoning does not hold — PS-8 forbids progression
> **ceilings**, not affordability, and `stamina`/`qi` already refuse through this exact
> `ActorResourcePools.TrySpend` path without anyone calling that a cap. The surviving `TryCommit`
> **refuses** (all-or-nothing), matching every other resource in the hub.

**`PoiseRuntime` is shaped after `ShieldRuntime`, deliberately.** Ideal §7 records that shields were
closed by **phase decomposition** — effective HP plus a gate on reflection — and that *"`poise` will
need the same treatment when it is registered."* [spec-deterministic-core.md](spec-deterministic-core.md)
§9.1 keeps the seam open for a second absorbing phase so adding one is not a rewrite.

---

## 8. Code style

**`long` for the pool and every drain.** It is a magnitude fed by `P(Θ)` through `Vigor`/`Bulwark`
edges. Widen before multiplying; divide by 1000 last.

**The riposte conversion is a ratio and says so:**

```csharp
// BOUNDED RATIO (PS-8 exempt): a fraction of poise spent, in [0,1]. Not a cap on damage -
// the poise it converts is uncapped, so the output is too.
const int RiposteShareCapPermille = ...;
```

That comment matters. `SoulEarnPolicy.VictoryFullPerDay` survived three cap sweeps because it refused
nothing and named a threshold — **a ceiling need not be a `const` nor be named like one.** A share of
an uncapped pool is genuinely a bounded ratio; a flat riposte value facing a scaling sink would be a
cap wearing a rate's clothes.

**The name.** `guard` is unavailable — it is the action layer's A8 category (the `block → guard` rename,
F2). `poise` collides with nothing in `src/`.

---

## 9. Testing strategy

| # | Test | Asserts |
|---|---|---|
| 1 | `Raising_a_guard_costs_even_when_nothing_lands` | Reading C's flat half. The rule §3 quotes, made executable |
| 2 | `Absorb_drain_is_proportional_to_what_was_stopped` | Reading C's proportional half |
| 3 | `Heavy_hits_break_the_guard_and_attrition_does_not` | §4's `r` low. **The FORCE → BASTION arrow, as an assertion** |
| 4 | `Poise_regen_never_exceeds_peer_pressure` | `r < 1`. A guard that cannot break is the termination defect |
| 5 | `Spent_poise_converts_to_damage` | §5 |
| 6 | `Riposte_scales_with_the_ladder` | An uncapped pool converting a bounded share stays uncapped. PS-8 |
| 7 | `Poise_at_zero_applies_exhaustion_not_death` | §2's table — `hp`'s exemption does not transfer |
| 8 | `Termination_invariant_holds_with_poise_live` | Re-run `balance-guard`'s hard half. **A new recovery source is exactly what could break it** |
| 9 | `Guard_costs_stamina_before_the_ADR` | The documented fallback works, so this module is not a hard block on the program. ⚠️ **Stale after the 2026-09-05 amendment above** — the ADR landed and `PoiseRuntime`'s own copy of this test's premise ("poise not yet registered") was already false before this note; `poise-unification`'s migration recorded it as not-applicable rather than porting a test with a false premise |

**Test 8 is the one that must not be skipped.** `poise` regen is a recovery term, and the termination
invariant is `damage − recovery`. Adding a recovery source without re-running the hard criterion is how
the one unfixable defect gets shipped.

---

## 10. Boundaries

**Always** — charge the flat cost on commit; drain proportionally on absorb; size regen against peer
pressure; re-run the termination guard after any regen change.

**Ask first**

- **Everything in this module, until the ADR lands.** The channel does not exist.
- The riposte share — it is BASTION's whole offence.

**Never**

- Register a `resource.max` channel for **poise** before the `decisions.md` amendment (§2).
- Price the guard on landing alone (§3).
- Let `poise` regen reach or exceed peer pressure (§4).
- Use the word `guard` for the resource (§8).
- Exempt `poise` from exhaustion the way `hp` is exempt (§2).

---

## 11. Success criteria

1. The ADR amendment has landed and `poise` is a registered resource with all seven registry fields.
2. Flat commit **and** proportional absorb, both asserted.
3. `r` measurably below 1 on the shipped tuning; heavy hits break, attrition does not.
4. Spent `poise` converts to damage; the conversion is a bounded share of an uncapped pool.
5. **The termination invariant re-run and green with `poise` live.**
6. Until the ADR, guard costs `stamina` and everything else is green.

---

## 12. Design-gate checklist

```
[x] Subsystems identified: resources, combat damage, shields, status (exhaustion), caps, tunables.
[x] Read this session: DESIGN-GATE.md, decisions.md (Resource model, Shield layer, Status SSOT,
    Combat mitigation shapes, Caps rows), resource-hub-ssot.md (FULL - §2 pays-for table, §5
    registry shape, §6 polarity, §10 exhaustion), ssot-power-scale.md §11 (incl. §11.2a inline
    caps and the VictoryFullPerDay lesson), class-system-ideal.md (§5.1, §5b.3, §5d.3, §8.3, §8.9).
[x] Every factual claim cites a document section.
[x] Read the surrounding section of every rule quoted - spec-action-costs.md §3 AND §7 together,
    because §7's shield exclusion is what makes §3 bite here; resource-hub §2's table under its own
    heading, which is where the "stamina pays for guard" fallback comes from.
[x] Constraints TESTED, not assumed - the block is real: DerivedStatChannels.ResourceIds is the
    locked five and SpecChannelClaimTests already went red on a `resource.max` claim for poise in this program's
    history. That is a run result, not a prediction.
[x] Nothing contradicts a §2 invariant. PS-8: the riposte share is a BOUNDED RATIO over an uncapped
    pool and §8 requires the comment saying so; the regen ceiling `r < 1` is a BALANCE target
    enforced by a test, not a hard clamp.
[x] Corrections propagated - §3's reading C, §4's regen and §5's riposte are recorded as decisions
    5-7 in ideal §0.0.1a and as module 9 in the map; all three land together.
```

---

## 13. Related

- [class-system-ideal.md](../class-system-ideal.md) §5.1, §5b.3 (cost), §5d.3 (the sizing rule), §8.3 (regen), §8.9 (riposte)
- [resource-hub-ssot.md](../resource-hub-ssot.md) §2, §5, §10 — the registry this amends
- [decisions.md](../decisions.md) — *Resource model*, the row the amendment edits
- [spec-balance-guard.md](spec-balance-guard.md) — test 8's consumer
