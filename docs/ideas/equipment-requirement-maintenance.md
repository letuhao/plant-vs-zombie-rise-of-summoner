# Equipment Requirement and Maintenance Engine

**Status:** Enriched approved idea. The audit findings below are resolved as
proposal decisions; this is still not an architecture lock, schema, or
implementation plan.

## Problem statement

How might Rise of Summoner let exceptional equipment demand an appropriate
build and continuing price without making ordinary loot unusable, creating a
second stat system, or allowing a strong item to be equipped without a tradeoff?

This extends the **item collection and progression** and **level up and power**
spine loops in [the-loops.md](../guide/the-loops.md).

## Current foundation

- Assignment currently checks role unlock, frame, level, and optional faction;
  it has no aptitude or resource requirement.
- The only primary-stat vocabulary is the twelve-aptitude catalog. An aptitude
  is an allocation source, not an item-grantable derived channel.
- The actor resource registry is shared and contains `hp`, `stamina`, `hunger`,
  `spirit`, `qi`, and `poise`.
- Item seeds are generator input: authored seeds name bands and validated
  vocabulary, while generated output owns magnitudes, weights, and quantities.
- Power magnitudes read `P(Theta)` from the common power ladder. No item system
  may introduce a private level-to-cost curve.

Sources: [item requirements SSOT](../architecture/item/ssot-requirements.md),
[item seed contract](../architecture/item/seed-contract.md),
[power-scale SSOT](../architecture/power/ssot-power-scale.md), and
[resource hub SSOT](../architecture/resource-hub-ssot.md).

## Recommended direction

Each concrete item may resolve to an optional requirement profile:

```text
no profile                 ordinary item, no restriction beyond current gate
level profile              optional specimen-level requirement
fixed-build profile        one or more aptitude-point minima
ratio-build profile        one or more individual aptitude-share minima
sustained profile          fixed or ratio build profile plus resource upkeep
```

Profiles are optional. A high-rarity item is not automatically demanding, and
an ordinary item may remain free of level, build, and maintenance requirements.

This makes room for an extremely rare low-cost exceptional item while ensuring
that a normal overpowered item pays a real maintenance tradeoff.

### Fixed and ratio build requirements

Both modes are supported:

```text
fixed: Might >= threshold
ratio: Precision's share of the actor's aptitude allocation >= threshold
```

Ratio mode reads **individual aptitude share**, not posture share. It directly
explains why the item favors a build and avoids a second layer over the existing
twelve aptitudes. Posture remains a display summary and is not a v1 requirement
axis.

Future requirement evaluation must read the existing aptitude allocation and
must never count effects supplied by equippable items. Gear must not enable its
own requirement.

### Resource maintenance

Maintenance is an ongoing obligation, not an equip gate and not an unequip
trigger:

```text
assigned and bound
  -> active while reserve and interval payment succeed
  -> suspended when payment cannot succeed
  -> active again when its reactivation condition succeeds
```

A suspended item stays equipped but contributes no item effects. This prevents
combat spending from causing an equip cascade while making strong effects carry
a meaningful price.

`hp` maintenance has a stricter recovery rule:

```text
activation / reactivation: current HP equals maximum HP
active:                    scheduled HP payment is affordable above its reserve
payment failure:           suspend every effect from that item
```

HP maintenance is non-lethal by default. A future exception would need explicit
approval and an item-level declaration.

## Deterministic Seedsmith resolver

The LLM and deterministic stages have different authority.

### LLM: classify only

The offline model may propose a **build-favor pool** from the base type, rolled
affix families, thematic tags, and derived power vector. Its output is limited to
closed vocabulary:

- favored aptitude ids;
- allowed requirement modes (`fixed`, `ratio`, `sustained`);
- resource-theme candidates; and
- presentation rationale.

It cannot author thresholds, probabilities, rates, weights, or a runtime rule.
The normal Seedsmith validator rejects any output outside those registries.

### Deterministic resolver: choose and calculate

The resolver is the only authority that creates a concrete profile:

```text
(catalog revision, item seed, content Theta, P(Theta), power vector,
 rarity, validated build-favor pool, tuning revision)
  -> exact requirement profile
```

It selects a profile from a weighted distribution, then resolves all numeric
thresholds, resource reserve, and upkeep rate from tuning. Identical inputs must
produce byte-identical requirements.

## Requirement distribution matrix

The tuning-owned matrix uses these dimensions:

```text
rarity band x resolved power band x build-focus strength
  -> weights for { none, level, fixed, ratio, sustained, jackpot }
```

- **Resolved power band** comes from the item power model and the common power
  ladder; it is the main maintenance-price input.
- **Only strong power bands may select maintenance.** Every lower band assigns
  the sustained profile zero weight. Rarity alone never makes an item pay
  upkeep.
- **Build-focus strength** measures how concentrated the item power vector and
  affix families are. A narrow defence or damage item can demand a matching
  build; a broad utility item need not.
- **Rarity** influences the distribution of profiles, not the arithmetic cost.
  This preserves rarity overlap while allowing high-rarity focused gear to be
  more often demanding.
- **Jackpot** is a deliberately low-weight profile for an unusually strong item
  with low or no maintenance. Its probability is data, not a hard-coded promise.
- Every selected maintenance profile maps its favor to a resource through tuning
  and uses the shared six-resource registry rather than a hand-maintained list.

The matrix is balance data. Its weights, thresholds, reserve bands, interval
cost bands, and jackpot floor live in versioned tuning files, not seeds or code.

## Resolved contract

The audit turns the direction above into the following proposed contract. It
keeps durable assignment separate from whether its effects are currently
active.

### Profile grammar and requirement evaluation

Version 1 has one selected aptitude target, never an ambiguous bundle:

```text
profile = {
  level:       none | minimum specimen level,
  build:       none | fixed(aptitudeId, minimumPoints)
                    | ratio(aptitudeId, minimumShareMilli),
  maintenance: none | upkeep(resourceId, reserve, cost, periodTicks)
}
```

`maintenance` may combine with any `level` or `build` arm, including `none`.
That makes a high-power broad item able to demand upkeep without inventing a
fictional build target. The distribution matrix selects this complete shape;
`sustained` is therefore not a competing, ambiguous requirement mode.

The selected `aptitudeId` comes only from the item seed's validated,
ordinally-sorted favored-aptitude pool. An empty pool may resolve only to a
profile without a build arm. A matrix row that selects a build arm for an empty
pool is invalid content and blocks minting; it never falls back to an invented
aptitude.

For a ratio check, runtime uses exact integer comparison rather than
`AptitudeAllocation.Share()`:

```text
aptitudePoints * 1000 >= grandAllocationPoints * minimumShareMilli
```

Both products are checked `long` arithmetic. `minimumShareMilli` is a bounded
per-mille ratio, and an empty allocation fails every positive ratio floor.
This preserves the allocation's "sum scopes, then share" rule without a
floating-point boundary case.

At assignment, the existing role/frame/faction/level gate remains the hard
gate. A **generated** fixed, ratio, or maintenance requirement never rejects
equipment assignment. It creates an equipped `trial` state with a clear route
to activation. After assignment, an unmet build or upkeep condition never
deletes the assignment: it keeps the item in `trial` or suspends its effects.
Re-evaluate it on loadout deployment, every aptitude-allocation change, and
before each maintenance charge. The existing optional-level projection behavior
is unchanged: a lapsed legacy level clause remains a reported shortfall rather
than silently removing a binding.

### Activation owner, state, and visibility

Introduce one future `EquipmentActivation` runtime owner. It is the only
component allowed to decide whether a durable equipment binding is active. The
profile and assignment are durable; activation is a deployment-run status keyed
by an opaque deployment key and durable assignment identity:

```text
(deploymentKey, specimenId, role, refKind, refId)
```

It owns for that deployment only:

- `state`: `trial`, `active`, or `suspended`;
- `reason`: `build_unmet`, `upkeep_shortfall(resourceId)`,
  `hp_recovery_locked`, or `set_trial_unmet`;
- `nextDueTick`; and
- the HP full-recovery latch.

`EquipAtomSource` remains the sole equipment-effect reader. It receives the
activation result and filters suspended bindings before composing either battle
or derived snapshots. No effect binding is deleted to suspend an item. A state
revision triggers the normal snapshot rebuild, so every atom from that one item
appears or disappears together. Removing an assignment, clearing a deployment
binding, or ending the deployment clears its status. A later deployment
re-evaluates the frozen profile with no inherited due tick, suspension, HP latch,
or debt.

The card and API expose the state, typed reason, and the cheapest known next
step. A player can equip a desired item immediately, then see an unmet build,
a failed resource payment, or the HP recovery lock rather than an unexplained
missing bonus or a hard refusal.

### Upkeep clock and payment order

Upkeep is processed on the deployment's simulation logical tick, not wall-clock time.
Activation schedules the first payment at `currentTick + periodTicks`; there
is no charge merely for equipping. The scheduler processes every due logical
interval, including after a delayed frame, and never charges for offline time.
Lawn status begins only at `PendingSpawn -> Bound`, freezes while paused, and
clears at binding or board end. A Delve deployment carries status across rooms;
rest can refill pools but never creates a back-charge. Equipment owns neither
its own clock nor pools. A siege engagement can be a deployment for an
individually equipped combat participant. World-map troop legions and siege
structures are not individually equipped participants, so they never activate
maintained gear or pay upkeep.

### Deferred deployment-scope refactor

The current owners are intentionally separate: the lawn uses `MatchRuntime` and
`UniqueBindings`, standalone battle uses `BattleEngine`, the Delve session owns
multi-room continuity, and siege will own an engagement resolver. Equipment
uses a thin adapter over each owner rather than creating a fifth FSM.

After all four adapters are proven, a cross-program refactor can introduce one
small shared deployment contract: opaque key, logical clock, participant
bound/cleared events, and actor-pool lookup. It must not absorb combat
scheduling, lawn observation, world-map movement, or durable actor lifecycle.
Each adapter migrates separately with its existing lifecycle tests kept green.

For each due interval, process active sustained items in this canonical order:

```text
specimenId ordinal, role ordinal, refKind ordinal, refId ordinal
```

An item payment is one frozen resource charge through the shared cost ledger.
It succeeds only when the post-payment value remains at or above that profile's
reserve and, for HP, its nonlethal floor. The ledger entry does not read action
rows, rungs, Theta scaling, or RNG. Items are charged independently in the order above;
when a shared resource is scarce, later items suspend predictably rather than
depending on container iteration. A suspended non-HP item reactivates at the
next evaluation only when its build requirement passes and it can satisfy the
next payment plus reserve.

HP uses the same payment order but adds a recovery latch. It is non-lethal:
the payment must satisfy both the existing HP floor and the profile reserve.
On an HP shortfall, set `hp_recovery_locked`. The latch clears only when, at
one evaluation instant, `currentHp == maxHp` from the same fresh derived
snapshot; a changed maximum HP simply changes what "full" means at that next
evaluation. After the latch clears, the item still needs its next scheduled
payment to remain above reserve. This makes full recovery necessary but never
pretends it alone pays the upkeep.

### Concrete-item determinism and model boundary

Requirements are materialized once, at item mint, and stored with the concrete
item together with their resolver and tuning revisions. Existing items are
never silently re-resolved after a tuning or catalog change. A migration is an
explicit future operation with an old-to-new profile report.

The deterministic resolver uses the repository's named-stream RNG, not
`System.Random`:

```text
SeededRng.DeriveStream(itemRollSeed, "item.requirements:v1:<draw-name>")
```

Each draw has its own fixed name (`profile`, `aptitude`, `threshold`,
`resource`, `reserve`, `period`) and each candidate collection is sorted by
its canonical id before drawing. A weighted draw rejects a non-positive total,
duplicate candidate id, unknown id, or missing tuning revision. This prevents
an extra future draw, JSON order, or a duplicate row from changing a specimen's
requirements.

The resolver input is now fully named:

```text
(itemRollSeed, resolverRevision, catalogRevision, tuningRevision,
 concrete content Theta, P(Theta), PowerVector, rarityId,
 validated persisted buildFavorPool)
  -> frozen concrete requirement profile
```

`P(Theta)` selects a tuning-owned power band; `PowerVector` selects a
tuning-owned focus band. These are the only power reads. Threshold and upkeep
bands are lookup data indexed by those bands, never a new formula from actor
level.

A model is an offline Seedsmith authoring aid only. It proposes a closed
build-favor label during seed generation; validation resolves that label to the
persisted favored-aptitude pool before the catalog revision is accepted. The
runtime resolver neither calls a model nor sees a model version. Missing,
invalid, or contradictory classification produces Seedsmith `blocked` output
and writes no seed or item.

### Rarity boundary and content validation

Rarity selects a row in the distribution matrix and may therefore change the
chance of `none`, `level`, `fixed`, `ratio`, `maintenance`, or `jackpot`.
After profile selection, the threshold/reserve/period lookup is indexed only by
the chosen profile, power band, focus band, and relevant favored aptitude or
resource. It does not accept rarity as an input. A resolver test must prove
that two otherwise-identical selected profiles have the same values across
rarities.

Before accepting a matrix revision, validation requires:

- positive total weight for every reachable matrix cell;
- each profile arm to have a complete threshold/upkeep lookup;
- legal resource and aptitude ids, unique candidates, and positive periods;
- a reachable ordinary (`none`) path and a separately configured jackpot path;
- all resolved magnitude arithmetic to remain checked `long`; and
- a blocked diagnostic, rather than a fallback, for every invalid or
  unsatisfiable cell.

## Set-safe requirement reconciliation

Requirements are a trial toward using equipment, never a mechanism that makes
a collected set impossible to wear. This matters because set bonuses are
derived from durable equipped bindings and a completed set is a real player
goal in the item-collection loop.

### One frozen plan for a whole set

Each set seed owns a `setRequirementPlanSeed`. At catalog generation, the
deterministic `SetRequirementReconciler` resolves one immutable envelope:

```text
(setId, setRequirementPlanSeed, catalogRevision, tuningRevision,
 set PowerVector, set P(Theta), member role list)
  -> SetRequirementEnvelope
```

The envelope contains one shared favored aptitude (or none), fixed and ratio
ceilings, one shared upkeep resource (or none), one total upkeep budget, and
one frozen member profile for every declared set role.

Every member profile is still seeded and may be `none`, but a member may use
only the envelope's shared aptitude and resource. Its fixed or ratio floor is
at or below the envelope ceiling. Its upkeep contributes to the set total,
rather than becoming an independently drawn second bill. All sustained members
use the same period. A full set therefore has one coherent target, not a
collection of unrelated demands.

The reconciler runs before any member item is minted and its result is stored
with the catalog revision. Owning, equipping, moving, or dropping a piece never
changes its requirements. This makes reconciliation replayable and prevents
loadout-order exploits.

### Full-set trial rule

Set counting still sees every legally equipped member, including one in
`trial`. This preserves the visible `N / total` progress and lets the player
assemble the complete set. A set tier's effects activate only when the set's
equipped members satisfy their one `SetRequirementEnvelope`. Until then, the
card shows the single next target, for example:

```text
Set trial: 4 / 4 equipped
Activate all set effects: reach Precision 640‰ and maintain poise
```

The values are illustrative. The rule is that the player gets one compatible
target, not four contradictory demands. Once satisfied, all valid member
effects and eligible set tiers activate together. A later lapse suspends those
effects together; the equipped pieces and set count remain intact.

This prevents a dormant full set from granting its capability for free, while
avoiding the worse experience of being unable to equip the last piece. The
trial is the compatible, visible route to activation; it is not a resource
price. When the set has maintenance, that maintenance is the tradeoff.

### Deterministic reconciliation rules

For an ordinary non-set item, the item resolver remains unchanged. For a set
member, it resolves only within the set envelope. Before accepting a catalog
revision, the reconciler validates:

1. Every member role has one frozen profile; no profile names an aptitude or
   resource outside the envelope.
2. The maximum fixed and ratio floors form one build direction: one favored
   aptitude, never opposing targets.
3. The sum of member upkeep is within the tuning-owned set budget, and every
   upkeep row has the same resource and period.
4. Existing role, frame, and slot-unlock rules permit the top set threshold at
   its declared level. A trial may not hide a structural slot impossibility.
5. A pure full-set activation check passes for a witness allocation and
   resource snapshot generated from the envelope. The witness is validation
   data, never a player grant or a progression cap.

Failure is `set_requirement_unsatisfiable`: Seedsmith blocks the set seed and
writes no member profiles. It does not weaken a live item, silently reroll a
requirement, or substitute a random target.

### Trial guidance, not automatic forgiveness

`EquipmentTrialPlanner` is a read-only deterministic projection over the
equipped loadout. It reports the minimal route to activation: the aptitude
target and missing fixed points or ratio share; resource reserve, cost, and
next payment tick; and, for a set, the shared envelope target and the member
currently preventing it.

It does not allocate points, refill resources, waive payment, reroll profiles,
or change an item. Maintenance is the price for strong power; the trial only
ensures the player can see and pursue a valid answer.

## Why this addresses mixed god builds

A strong focused defence item no longer asks only whether the wearer is a high
enough level. It can require a defensive aptitude allocation or a sustained
poise commitment. A damage-focused actor can still choose it, but must change
its allocation, carry the upkeep, or accept suspension. The player makes a
visible build choice instead of stacking every strongest item at no cost.

## Audit resolution and remaining validation

The initial audit found undefined ownership, time semantics, profile
composition, replay inputs, model lifecycle, and recovery behavior. The
sections above resolve those as one proposed contract. The remaining work is
verification, not a missing rule:

- prove the existing item power reads can supply the named bands without
  duplicating the power model;
- prove the unique-specimen allocation is available at each evaluation point;
- add the activation filter at the existing `EquipAtomSource` seam without a
  second effect-delivery path;
- wire one logical-tick caller in every supported runtime before enabling any
  sustained profile; and
- simulate matrix rates and roster builds to tune ordinary-item and jackpot
  frequency.

## MVP scope

1. Frozen deterministic profile resolution for one item source.
2. One fixed and one integer-share aptitude requirement.
3. One non-HP upkeep resource, then the HP recovery latch.
4. Activation filtering through `EquipAtomSource` and deployment lifecycle recovery.
5. Set-envelope reconciliation with a full-set witness test.
6. Seeded replay, ordering, suspension, and matrix-distribution reports.

## Not doing

- No new five-stat attribute system.
- No resource value as a hard equip or force-unequip gate.
- No generated build, ratio, or maintenance requirement as a hard equip gate;
  these are activation trials.
- No direct rarity-to-cost multiplier.
- No persistent negative resource debt or repayment trap.
- No posture-share requirement in the first version.
- No LLM-generated numbers or runtime decisions.
- No schema, runtime, or tuning implementation before the audit closes the
  assumptions above.

## Audit agenda

1. Test identical concrete inputs, candidate ordering, and extra unrelated RNG
   draws for byte-identical frozen profiles.
2. Test every activation transition, save/load at a due tick, and shared
   resource contention in canonical order.
3. Test ratio equality, empty allocations, changed maximum HP, and checked
   overflow boundaries.
4. Compare focused damage, defence, utility, and hybrid builds for unwanted
   dominance or mandatory maintenance resources.
5. Prove every set can reach its top threshold and activate through its frozen
   envelope without profile conflict.
6. Measure the profile matrix against loot usability and jackpot frequency.
