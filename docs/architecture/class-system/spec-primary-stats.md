# Spec: `primary-stats` — the twelve, as a shipped thing

**Module id:** `primary-stats` · **Program:** [class-system-map.md](../class-system-map.md) ·
**Status: AUTHORIZED 2026-08-26 -- owner's /goal directive commands execution of the class-system plan to completion; supersedes this "awaiting owner review" header, which was never flipped after that directive landed.**

**Depends on:** nothing · **Blocks:** `aspect-scope`, `aptitude-tuning`, and through them everything else

---

## 1. Objective

Make the twelve primary stats **exist** — as a closed id set, a value type an actor can hold, and three
classification decisions the rest of the program reads rather than re-derives.

**Why it is module 1, and why it was missing.** Verified this session: `aptitude` appears **nowhere** in
`src/`, `tests/`, `data/` or `web/`. It lives only in `tools/CombatSim/`, which ships to no player. The
previous map opened with `aptitude-tuning` — a config file of coefficients for stats that have no home,
feeding a resolver that reads points nothing persists. Eleven modules named "the aptitude" and none of
them declared it.

**Users:** every other module in this program; the actor sheet; `residual-fit`, which compares the
shipped module against the POC and therefore needs both to mean the same thing by the same word.

**Success is measurable:** an actor can hold an allocation across four scopes, the sum resolves to a
`share` per aptitude, and a thirteenth aptitude is unrepresentable rather than merely undocumented.

**What this module does NOT do.** It does not resolve a point into a channel (`aptitude-resolve`), does
not decide what a point is worth (`aptitude-tuning`), and does not decide how many points anyone gets
(`point-economy`). It declares the noun those three all take as an argument.

---

## 2. The twelve

Four per posture, from [class-system-ideal.md](../class-system-ideal.md) §4. **Posture is a grouping
for humans and a shape for Zomboss patterns — never a container the player is placed in**, so §2.2 makes
it a derived read rather than a stored field.

| Posture | Aptitude | Mechanism role | In one line |
|---|---|---|---|
| **FORCE** | `Might` | universal offence — `power` | Hit harder. |
| | `Fortitude` | Mitigation — `defense` · `absorption` · `reduction` | Take less of everything. |
| | `Vigor` | Shield — `shield.capacity/regen/toughness` | More to lose before you lose. |
| | `Onslaught` | breaks Guard + Reflect | Their guard stops mattering. |
| **FINESSE** | `Agility` | Dodge | Be somewhere else. |
| | `Composure` | Crit-denial | Nothing lands clean on you. |
| | `Pierce` | breaks Mitigation + Shield | Armour stops mattering. |
| | `Focus` | utility — `qi`, efficiency, cooldowns | Do it again, sooner, cheaper. |
| **BASTION** | `Bulwark` | Guard — `parry`/`block` rate and strength | Stop it outright, sometimes. |
| | `Retribution` | Reflect | Hitting you costs them. |
| | `Precision` | breaks Dodge — `accuracy` | They cannot dodge. |
| | `Ferocity` | breaks Crit-denial — `crit.rate/damage` | Sometimes it is much worse. |

**The one-line reading is part of the contract, not flavour text.** Under free build there is no class
name to carry meaning, so it is the entire identity a player gets (ideal §4.3). It lives beside the id.

> **Twelve is a measured outcome, not a decision** (ideal §4.3). A system has as many primary stats as
> pass the free-build test — *every aptitude is the best point somewhere, and none everywhere*. This
> module ships twelve because that is the current answer; `balance-guard` re-answers it in
> milliseconds after every coefficient change, and **the id set is versioned so shrinking it is a
> migration rather than a rewrite**.

### 2.1 The naming collision — resolved here, binding on all eleven modules

**`primary` is already taken, in this exact subsystem, meaning something else.**

- [stat-system.md](../stat-system.md): *"**StatSystem** composes **primary** channels only"* — the
  Unity combat baseline, `hp` `maxHp` `atk` `defense` `arm1` `arm2`.
- [design/spec-magnitude-and-units.md](../../design/spec-magnitude-and-units.md) §3: *"Every derived
  **and primary** magnitude belongs to exactly one class."* Same sense.

[DESIGN-GATE.md](../../DESIGN-GATE.md) §1's *Stats* row was widened after a session invented a parallel
classification against these two documents. **The split:**

| Word | Means | Where it is legal |
|---|---|---|
| **primary stat** | the twelve | **player-facing text, and the program / module id** |
| **aptitude** | the twelve | **all code, all ids, all config keys, every spec in this program** |
| **primary channel** | `StatSystem`'s Unity baseline | shipped code — **untouched by this program** |

Nothing in shipped code moves. `aptitude` is already `tools/CombatSim`'s word, so the POC and the
shipped module agree on day one — which `residual-fit` depends on.

### 2.2 Posture is derived, never stored

Storing a posture on an actor re-creates the class the owner removed on 2026-08-25. A posture is a
**region of the allocation space**: an actor is FORCE-leaning because its points are there, and it stops
being FORCE-leaning the moment they move.

```csharp
// A read over the allocation. Never a field, never persisted, never a branch in resolve.
public static Posture DominantPosture(AptitudeAllocation a);   // ties resolve to None
```

It exists because Zomboss patterns and the UI both want to say "this thing is a bruiser". Nothing in
`aptitude-resolve` may read it.

---

## 3. Three decisions this module owns

### 3.1 An aptitude is a SOURCE, not a registered channel — and that is protective

Aptitudes **reach** 84 of the 259 registered channels (256 → 259, `poise-resource` landed 2026-08-26 —
three resource pool channels, none aptitude-fed today) — **counted from the shipped edge list
2026-08-26**; ideal §4.2 says 83 and is off by one. They are not among them.

**Consequence, and it is the reason the decision matters rather than being taxonomy:**

> `share = (points in this aptitude) / (points across all aptitudes)`
> ([spec-aptitude-tuning.md](spec-aptitude-tuning.md) §2.1).
>
> **The denominator is the actor's own total.** If an aptitude were a derived channel, an item granting
> `+5 Might` would compose through the modifier bag — raising the numerator *and* the denominator, so it
> would **silently reduce every other aptitude's share.** An item that reads as a pure bonus would be a
> nerf to eleven other stats.

So `+5 Might on an item` is a deliberate later decision with a known cost, not a free consequence of
registering a channel. Recorded rather than discovered when the first such affix is authored.

**What follows:**

| | Because |
|---|---|
| Not in `DerivedStatCatalog`; `CreateDefault()` unchanged | Not a channel |
| `guard-stat-pairs.ps1`'s counterpart rule does not apply | That rule is about contest-class *channels* |
| `SpecChannelClaimTests` does not fire on an aptitude id | Nothing is being claimed from the catalog |
| **A new guard is owed** (§7, guard 2) | Nothing else would catch an aptitude id colliding with a channel id |
| The four `statClass` values (contest · race · pool · feeder) do not apply | They classify what a formula *reads*. An aptitude is what a coefficient reads *from* |

### 3.2 The render class — `AptitudePoints`, **authorised 2026-08-26**

[design/spec-magnitude-and-units.md](../../design/spec-magnitude-and-units.md) §3 is a ten-class ledger,
and guard 1 there refuses a renderable magnitude with no class. **A primary stat is renderable and has
no class today.**

| Candidate | Verdict |
|---|---|
| `Count` | Renders `2 bullets`, **no context part**. But the entire value of "Might 55" to a player is what it buys, and `Count` structurally cannot say |
| **`AptitudePoints`** (new) | **Chosen and authorised.** The `LadderIndex` precedent is exact |

**Why the precedent is exact.** `LadderIndex` was added 2026-08-24 for `Θ`, because *"Θ is read two
different ways, and the player needs both"* — contests linearly, magnitudes through `P(Θ)`. **An
aptitude is read by those same two functions**, and for the same reason: PS-3
([power/ssot-power-scale.md](../power/ssot-power-scale.md) §4.6). Showing only the point count hides
what it does; showing only the effect hides that contests do not compound.

**One difference, and it decides where the context part is allowed.** `LadderIndex`'s context part is
**exact** — `P(20) = 680` is the shipped pin. An aptitude's is not, because `share` divides by the
actor's *whole* allocation: `Might 55` alone buys nothing determinate. So it follows §4.2's two-reference
rule instead:

| Surface | Context part |
|---|---|
| **Actor sheet** — an allocation is selected | **Allowed and exact given that allocation.** `Might 55` · `→ +2,200 omni power` |
| **A card, tooltip or anything with no allocation** | **Suppressed**, like `StatusPotencyPoints` (§4.3). A number computed against an allocation the player has not made is not a hedge, it is a fiction |

**AUTHORISED by the owner 2026-08-26**, on the same terms `LadderIndex` got — it is a contract change: the
`UnitClass` union in [contract/types.ts](../../../web/fusion-rpg-web/src/contract/types.ts) gains
`"aptitudePoints"`. Flagged, not assumed.

### 3.3 `element_mastery` — inherited, and it is **not** a primary stat

[derived-stats-map.md](../derived-stats-map.md) §7 deferred it *"with primary stats"*. Answering it is
this module's job; **building it is not.**

**It is not one of the twelve, and the reason is a rule this program already carries.** Ideal §4.1
rule 2: *an aptitude reaches a **mechanism**, never a **flavour**. Elements are flavours, so aptitudes
stop at `omni`.* `element_mastery` is per-element by definition
([chaos-derived-stats-audit.md](../../research/chaos-derived-stats-audit.md) §3.6 — every formula there
is `base + element_mastery × k`). A per-element progression axis is the flavour tier.

> **Verdict: `element_mastery` belongs to the `aspect` tier** — the element typing that
> [`aspect-scope`](../demons/spec-aspect-scope.md) is moving off the species (ideal §7c.4). It is that tier's
> progression axis, not a thirteenth aptitude.

**Two conditions handed forward with it**, so whoever builds it does not have to rediscover them:

1. **It owes a §10 row or a proof it is not power-shaped.**
   [power/ssot-power-scale.md](../power/ssot-power-scale.md) §10 is a **closed** inventory — *"a
   power-shaped number that is not in this table does not have permission to exist"*. `base + mastery × k`
   is exactly the shape a private `f(level)` wears when it is about to become the fourth incompatible
   curve.
2. **PS-3 applies to it too.** If mastery feeds a contest it reads linearly; if it feeds a magnitude it
   reads `P(Θ)`. It may not feed both through one number.

The `derived-stats` handover is closed by this section. Nothing is left implicit in it.

---

## 4. Commands

```powershell
# Tests
dotnet test tests\FusionRpg.Core.Tests --filter Aptitude

# Guards this module must not break
.\scripts\guard-single-writer.ps1
python scripts\audit-magic-numbers.py --domain aptitudes    # must find nothing: this module has no balance numbers
python scripts\audit-overflow.py --targets A3

# The POC this module must agree with, by vocabulary and by arithmetic
cd tools\CombatSim
dotnet run --no-build -- marginal -a force-ns,finesse-ns,bastion-ns --theta 100
```

---

## 5. Project structure

```text
src/FusionRpg.Core/Stats/Aptitudes/Aptitude.cs             the closed enum + id strings
src/FusionRpg.Core/Stats/Aptitudes/AptitudeCatalog.cs      id -> posture, role, one-line reading
src/FusionRpg.Core/Stats/Aptitudes/AptitudeAllocation.cs   the value type: points, sum, share
src/FusionRpg.Core/Stats/Aptitudes/AllocationScope.cs      commander | demonType | aspect | uniqueDemon
tests/FusionRpg.Core.Tests/Stats/Aptitudes/AptitudeCatalogTests.cs
tests/FusionRpg.Core.Tests/Stats/Aptitudes/AptitudeAllocationTests.cs
```

**No file I/O, no DB, no Unity.** The catalog is code-first, matching `StatusCatalog`'s shipped shape
(`decisions.md` *Status SSOT*: *"in-memory Core registry, code-first; no runtime YAML loader v1"*).
Persistence of an allocation is `point-economy`'s, in `FusionRpg.Data`.

**No `data/tuning/` file.** This module contains **no balance number** — that is the whole of
`aptitude-tuning`. If a number appears here during the build, it is in the wrong module.

---

## 6. Code style

```csharp
/// <summary>
/// A player's points across the twelve aptitudes at ONE allocation scope.
/// Immutable. An actor's effective allocation is the SUM of four of these (ideal §7c).
/// </summary>
public sealed record AptitudeAllocation
{
    // long, not int: points accrue proportionally to Theta (grant.aptitudePointsPerTheta x Theta),
    // and PS-8 forbids a progression ceiling, so the total is unbounded by design.
    // At 3 points/Theta an int overflows near Theta = 715,000,000 - reachable, not hypothetical.
    private readonly long[] _points;   // indexed by (int)Aptitude, length 12

    public long PointsIn(Aptitude a);
    public long Total { get; }                       // the share denominator
    public static AptitudeAllocation operator +(AptitudeAllocation a, AptitudeAllocation b);

    /// <summary>
    /// Fraction of this allocation sitting in one aptitude. Bounded [0,1] by construction -
    /// a BOUNDED RATIO, not a cap (PS-8 exempt: you cannot spend more than all of your points).
    /// </summary>
    public double Share(Aptitude a);
}
```

**Four rules, each with a reason that is not style:**

1. **`long` for points.** Rule 1 of the overflow standard: any magnitude `contentScale` can touch. The
   comment above is the PS-8 exemption note the standard requires.
2. **`double` for `share` only.** It is a bounded ratio in `[0,1]`, never a magnitude — the one place
   `double` is correct here.
3. **`Total == 0` yields `Share == 0` for every aptitude — never `1.0/12`.** An unallocated actor gets
   **nothing**, not an even spread. A `1/12` default would hand every fresh actor a full generalist
   build for free and make `point-economy`'s first point worthless. The zero guard is a **structural
   limit** (division by zero is a crash, not a balance outcome), exempt from PS-8 and commented as such.
4. **The four scopes SUM before `share` is taken.** Computing `share` per scope and combining afterwards
   is a different and wrong game: a commander point and a unique-demon point would not be
   interchangeable, and ideal §7c.2's weighting would be applied twice.

---

## 7. Testing strategy

`tests/FusionRpg.Core.Tests`, xUnit, constructed inline — no fixture files.

| # | Test | Asserts |
|---|---|---|
| 1 | `Twelve_aptitudes_exactly` | The enum, the catalog and the posture grouping agree, **computed** (`4 × 3`), with the literal `12` as a deliberate canary beside it — the shape `DerivedStatRegistryTests` already uses |
| 2 | `Every_aptitude_has_a_posture_a_role_and_a_reading` | No entry ships with a blank identity. Under free build the one-line reading is the whole identity a player gets |
| 3 | `Aptitude_id_never_collides_with_a_channel_id` | Cross-checked against `DerivedStatRegistry.CreateDefault()` **and** the prefix families. §3.1 removes aptitudes from the catalog's guards, so this is the guard that replaces them |
| 4 | `Share_sums_to_one_when_anything_is_spent` | Within `double` tolerance, over randomised allocations |
| 5 | `Empty_allocation_shares_are_zero_not_one_twelfth` | §6 rule 3. **The test that stops a fresh actor being a free generalist** |
| 6 | `Scopes_sum_before_share_is_taken` | `Share(a + b) != (Share(a) + Share(b))/2` on an asymmetric pair — §6 rule 4, stated as the inequality it actually is |
| 7 | `Allocation_is_immutable_and_addition_is_commutative` | `a + b == b + a`; no mutator exists |
| 8 | `Points_are_long_and_do_not_overflow_at_high_theta` | An allocation at `Θ = 10^9` (3×10⁹ points) is exact. **Red today with `int`** — the reason rule 1 is a rule |
| 9 | `Dominant_posture_is_a_read_not_a_field` | Moving points changes the posture with no other write; ties resolve to `None`. §2.2 |
| 10 | `Catalog_matches_the_simulator_vocabulary` | The twelve ids equal `tools/CombatSim`'s. `residual-fit` compares the two engines and a vocabulary drift makes that comparison meaningless |

**Tests 5, 6 and 8 are the ones worth arguing for.** Each encodes a decision that is invisible once
made and expensive once shipped: a default share, a scope-combination order, and an integer width.

---

## 8. Boundaries

**Always**

- Use **`aptitude`** in code, ids and config; **"primary stat"** only in player-facing text (§2.1).
- Keep the id set closed and versioned — adding or removing one is a migration.
- Comment every bounded ratio and structural limit with its PS-8 exemption class.
- Ship the one-line reading beside the id, not in a separate content file.

**Ask first**

- **Adding `AptitudePoints` to the `UnitClass` union** (§3.2) — a contract change, and `LadderIndex` set
  the precedent that it takes an explicit authorisation.
- **A thirteenth aptitude, or removing one of the twelve.** The count is a measured outcome, so this is
  a real possibility rather than a hypothetical — but it moves every coefficient in `aptitude-tuning`.
- Making an aptitude grantable by an item (§3.1's denominator consequence).

**Never**

- Register an aptitude in `DerivedStatCatalog` (§3.1).
- Store a posture on an actor (§2.2).
- Put a balance number in this module (§5) — it belongs in `aptitude-tuning`.
- Default an empty allocation to an even spread (§6 rule 3).
- `int` or `float` for points (§6 rule 1).
- Treat `element_mastery` as a thirteenth aptitude (§3.3).

---

## 9. Success criteria

1. `Aptitude` is a closed set of twelve with posture, role and reading, and test 1 computes the count.
2. `AptitudeAllocation` is immutable, `long`-backed, addable across four scopes, and takes `share` on
   the sum.
3. An empty allocation yields zero shares, asserted.
4. No aptitude id collides with any registered channel id or prefix family, asserted.
5. The twelve ids match `tools/CombatSim`'s exactly, asserted.
6. `python scripts/audit-magic-numbers.py --domain aptitudes` finds nothing — this module holds no
   balance number.
7. **Zero goldens move.** Declaring a type changes no behaviour; if a golden moves, something was wired
   that should not have been.
8. §3.3 is recorded in [derived-stats-map.md](../derived-stats-map.md) §7's row, so the handover is
   visibly closed rather than silently answered here.

---

## 10. Open

**10.1 ~~The `UnitClass` addition needs an authorisation~~ — GRANTED 2026-08-26, and this module owns
the edit.** `AptitudePoints` joins the ledger (§3.2), alongside `ReciprocalPoints` from
[spec-unit-class-close.md](spec-unit-class-close.md) §3.5 — **ten classes become twelve**.

**It is three strings, not two.** [contract/types.ts](../../../web/fusion-rpg-web/src/contract/types.ts)
declares **nine** members: `"ladderIndex"` has been owed since **2026-08-24**
([design/spec-magnitude-and-units.md](../../design/spec-magnitude-and-units.md) §3's own *"Contract
change owed"* note) and was never done. **This module lands all three in one edit** — a union touched
three times by three modules is exactly how the first one came to sit undone.

**10.2 Whether the twelve survive their own test.** Ideal §4.3 is explicit that the count is measured,
not chosen, and the most recent measurement is superseded (§0.0.5 there). This module ships twelve and
makes shrinking them a migration. **That is the honest posture, not a hedge** — and it is why success
criterion 1 computes the count rather than asserting a literal.

---

## 11. Design-gate checklist

```
[x] Subsystems identified: stats, power scale, tunables, resources, status (vocabulary only), UI contract.
[x] Read this session: DESIGN-GATE.md, decisions.md (all 98 rows), stat-system.md,
    design/spec-magnitude-and-units.md (full), derived-stats-map.md (full),
    resource-hub-ssot.md (full), tunables-ssot.md (full), ssot-power-scale.md
    (§4.6 PS-3, §4.7, §10.1-10.3 inventory), class-system-ideal.md (§0.0, §4-4.4, §7b, §7c, §8.8),
    spec-aptitude-tuning.md (full).
[x] decisions.md checked - Stats, Stat compose, Power scale, Caps, Magic numbers, Actor Hub SSOT,
    Resource model and Status SSOT rows all bear on this; none is contradicted.
[x] Every factual claim cites a file or a document section.
[x] Verified against CODE and DATA, not comments. The "29 of 50 unitClass nulls" and "3 statClass
    nulls" were COUNTED from data/seed/derived-stats/catalog.json this session, not quoted from the
    old map. The "aptitude appears nowhere in src/" claim is a grep over src/, tests/, data/, web/.
[x] Read the surrounding section of every rule quoted - PS-3 from §4.6, the "primary channel" wording
    from stat-system.md's own heading, §4.2's two-reference rule in spec-magnitude-and-units.md.
[~] Constraints tested, not assumed. PARTIAL - the two data claims above were verified by running a
    count. The "zero goldens move" success criterion is a PREDICTION about unbuilt code, not a
    measurement, and is written as a criterion rather than a finding.
[x] Nothing contradicts a §2 invariant. Invariant 11 (no hard ceilings): `share` is a BOUNDED RATIO
    and the zero-total guard is a STRUCTURAL LIMIT - both stated in §6 with their exemption class,
    per the rule that an exemption must say so in a comment. Points themselves are uncapped `long`.
[x] Corrections propagated. The map's build order, §2a and §5 land with this spec; §3.3 owes an edit
    to derived-stats-map.md §7, listed as success criterion 8 rather than left implicit.
```

---

## 12. Related

- [class-system-map.md](../class-system-map.md) — the program, and §5's reserved sub-features
- [class-system-ideal.md](../class-system-ideal.md) §4 (the twelve), §4.2 (what they do not reach), §7c (the four scopes)
- [spec-aptitude-tuning.md](spec-aptitude-tuning.md) §2.1 — the two read functions this type feeds
- [stat-system.md](../stat-system.md) · [actor-hub-ssot.md](../actor-hub-ssot.md) — the "primary channel" this must not collide with
- [design/spec-magnitude-and-units.md](../../design/spec-magnitude-and-units.md) §3 — the ten-class ledger §3.2 extends
- [power/ssot-power-scale.md](../power/ssot-power-scale.md) §4.6 (PS-3), §10 (closed inventory), §11 (PS-8)
- [derived-stats-map.md](../derived-stats-map.md) §7 — the handover §3.3 closes
