# Spec: action-model (A1)

Module **A1** in the [action map](../action-map.md). The foundation: **the action data structure, its tables, and its dataflow.** Every other module in the program is a consumer of what is defined here.

Depends on nothing that is not already shipped. The atom program's `effect_container.container_id` exists (`RpgStore.Containers.cs`), so this module's dependency on it is **one foreign key and zero API surface**.

> **Sealed inputs.** The owner decisions in [action-map.md](../action-map.md) §10.6 are settled and this spec does not reopen them. Where this spec and the map disagree, the map wins.

## Objective

Define what an action **is**, where it is stored, and what happens between "something decided to use this" and "the effects landed."

An action is the join row between three programs:

```
action = envelope (when)  +  container of atoms (what)  +  target rule (who)
         + costs (what it takes)  +  usability condition (may I)
```

**The membership rule** — the test that decides whether something is an action at all:

> Anything an actor does that interacts with the environment or itself, costs resource or time, and needs a cooldown, **is an action. No exception.**

Attack, skill, **summon**, move, block, and pass are actions. Passive traits, status pulses, and exhaustion debuffs are not — the actor does not *do* them.

## Design (locked on approval)

### 1. `rpg_action` — one row per action

Every value that scales is a **`ValueSpec`** (`Core/Effects/Atoms/ValueSpec.cs`), reused rather than redefined, so the atom program's `effect_curve` serves actions too and no second scaling mechanism exists.

| Group | Columns |
|---|---|
| identity | `action_id` PK · `name` · `tags_json` · `enabled` · `revision` |
| effects | `container_id` → `effect_container(container_id)` |
| timing | `time_cost_ticks` · `speed_channel` · `windup_ticks` · `resolve_offsets_json` · `recovery_ticks` · `commitment` · `interruptible` · `interrupt_refund_milli` · `slot_consuming` · `priority_band` |
| cooldown | `cooldown_class` · `cooldown_key` · `cooldown_ticks` · `starts_at` · `interrupt_cooldown_milli` |
| targeting | `target_spec_json` · `min_range` · `max_range` · `range_channel` · `anchor_source` · `requires_line_of_sight` |
| usability | `conditions_json` |

**`tags_json`** is a closed set — `offensive · defensive · heal · buff · debuff · movement · summon · utility`. It is what `A7` chooses on, and the atom program's standing rule applies: **AI reads tags, never internals.**

**The timing columns are the existing `ActionEnvelope`**, not a second copy of it. `A5` folds in the three gaps the map identified (`min`/`max` duration bounds, a cooldown-reduction channel, `interrupt_cooldown_milli`); this spec reserves the columns so that fold is additive.

### 2. `rpg_action_cost` — `(action_id, resource_id, amount_spec, when)`

A table, not columns, because an action costs several resources. `amount_spec` is a `ValueSpec`.

`when` is **`onCommit`** (default) or **`perTick`**. Per-tick is the Diablo channel shape: pay each resolve offset, and **failing to pay ends the action** through the existing interrupt path — cancel remaining resolves, release the slot, charge `interrupt_cooldown_milli`.

**Consumption is atomic.** Validate every cost, consume every cost, roll back all of them if any fails. An action that consumed stamina and then found no spirit must leave the actor exactly as it found them.

**Committing is what costs, not landing.** An interrupted channel has paid and does cool down; a fizzled action has paid; a missed attack has paid. One rule, no exceptions, and it is what keeps slot accounting identical on every exit path.

### 3. `rpg_action_effect_scope` — `(action_id, atom_id, scope)`

Which of the action's atoms hits whom. `scope` is a closed enum: **`caster` · `primaryTarget` · `eachTarget` · `casterAllies`**.

This is the "strike an enemy and heal yourself" problem — one action, two atoms, two recipients. Kept **action-side deliberately**: putting `scope` on `effect_container_atom` would change a sealed contract and make atoms less reusable outside actions.

Rows are optional; an atom with no row defaults to `eachTarget`.

### 4. Intra-action effect ordering — the one genuinely open cross-program question

`ContainerAtomRow.Seq` is documented as *"authoring order… **not an execution guarantee** — execution order belongs to the actor's effect list, which sorts by priority across every container it holds."*

**Proposed resolution: an action applies its own atoms directly at its resolve tick, and they never enter the actor's effect list.**

This is not a bypass — it is a different population:

| Population | What it is | Ordering |
|---|---|---|
| **Standing grants** — items, traits, species passives | Effects that *wait* for a trigger | The actor's effect list, priority-sorted across containers. Unchanged |
| **An action's own atoms** | Effects that *are* the event | Container `seq`, applied as one unit at the resolve tick |

It matches how the genre works: your skill's effects happen, and then your on-hit passives react to them. It also makes `"heal yourself for the damage this dealt"` expressible, which a global priority sort cannot guarantee.

**✅ Resolved 2026-08-22 — and the atom program had already answered it.** Two things in their sealed docs settle this, neither of which is a preference:

**1. Their model says the action is the event, not a responder.** `definitions.md`: *"An actor attacks. The attack raises an event. An atom on that actor's effect list responds."* The attack is not on the list — it is what the list reacts to.

**2. The trigger vocabulary has no way to express "because I chose it."** The seven triggers are `OnSpawn`, `OnDamageDealt`, `OnDamageTaken`, `OnDeath`, `OnGranted`/`OnRemoved`, and `OnTimer`. **Every one is reactive.** There is no `OnActionUsed` and no `OnCast`, so an action's own atoms *cannot* be list responders — they would have nothing to respond to. Making them so would need an eighth trigger, which is a reviewed change to a closed list.

So the two populations in the table above are not a design choice this spec makes; they are what the atom vocabulary already describes.

**One refinement worth carrying**, because it makes the effect list less hostile than it first appeared: their execution order is `(priority DESC, container_id ASC, seq ASC)`, compared **ordinal**. Since `container_id` is the second key, atoms from one container are **contiguous and in `seq` order** at equal priority. `seq` is not an execution guarantee across the whole list, but it *is* one within a container — which is exactly the guarantee an action needs, and it means `rpg_action_effect_scope` is implementable either way.

Related and to be verified rather than assumed: `IcdKey` **merges atoms sharing a key into a single grant with a shared clock**. An action whose atoms merge that way is one grant, not several, which interacts with per-atom scope above.

### 5. Where actions come from

| Source | Rule |
|---|---|
| **Intrinsic** | The species row names them. Every actor has a basic attack whether or not any content exists — a default must never depend on authored data |
| **Granted** | Species, learned skills, items, traits. Reuses `effect_binding`'s owner vocabulary; no second binding concept |

### 6. Dataflow

```
author rows (SQLite, server)
  → compile + push                                            [A6]
  → IIntentSource selects (action_id, target)                 [A7]
  → usability: predicate · cooldown · range · affordability   [A4]
  → commit:  validate all costs → consume all → roll back on any failure
             acquire slot (if slot_consuming)
             schedule resolve handles                          [kernel, shipped]
  → resolve: resolve target set per `commitment`               [A2 → TargetResolver, shipped]
             for each atom × scope → apply
             → DamageApplyPipeline / status / spawn            [shipped]
  → finish:  release slot · start cooldown · schedule recovery [kernel, shipped]
```

Only two links in that chain are new: **the typed target contract** (`A2`) and **the cost table** (§2). Everything else calls something that ships.

### 7. Grid parameters are authored now and inert until `A10`

Range is **not retrofittable** — adding `max_range` after actions are authored rewrites every row and every balance number set assuming infinite reach. So the columns land now, and:

> **With no board, every range check passes.** Not an error, not an empty result.

That is precisely what lets `A5` add these columns and still be **byte-identical**. A range check that throws or excludes when coordinates are absent breaks the freeze.

`min_range` / `max_range` are Chebyshev cells — two numbers, because a minimum cannot be retrofitted either.

### 8. `commitment` governs the target **set**, not one pointer

- **`EarlyBound`** — resolve the target set once at commit, reuse for every offset, fizzle per target as each dies.
- **`LateBound`** — re-resolve the whole set at each offset. What a spinning-blade multi-hit wants and a targeted combo does not.
- **`EarlyBoundWithFallback`** — as EarlyBound, retargeting instead of fizzling.

### 9. Placement — outside the tick-path guard, but **not** outside the purity guard

`TargetResolver` uses `.Select(`, `.Where(`, `.ToList(`, `.Take(`, all banned by the kernel's tick-path rules, and it allocates per call. The split is already correct — the kernel schedules and holds a `TargetKey`; it never resolves — so the action runtime lives in **`Core/Actions/`**.

**But moving out of the folder currently drops determinism enforcement as well, and that is a defect** (audit C1). The guard carries two rule sets, and only one of them is unwanted here:

| Rule set | Wanted in `Core/Actions/`? |
|---|---|
| `BannedOnTickPath` — LINQ, scene scans, stat resolves | **No** — the resolver needs LINQ |
| `BannedEverywhere` — wall clock, ambient `Random`, `Guid.NewGuid`, `.GetHashCode(`, floating point, dictionary enumeration | **Yes** — these are what byte-identical replay rests on |

`KernelPurityScan.Scan` runs on exactly one directory today. A `DateTime.UtcNow` or an ambient `Random` in the action path would compile, pass CI, and silently break every replay — and the action path is *more* exposed than the kernel, because it makes decisions rather than only scheduling them.

> **`Core/Actions/` is scanned with purity rules, tick-path exempt.** The mechanism exists — `DiagnosticsExemptFromTickPath` already does exactly this for `BattleTrace.cs`. It is a directory plus an exemption entry, not new machinery.

### 10. Action binding — how an actor holds an action

`effect_binding` points at an `instance_id`, not an `action_id`, so it cannot answer *"which actions does this actor hold"* on its own (audit I3). Both `A4`'s first gate and `A7`'s per-actor hoist depend on that answer.

| Source | Shape |
|---|---|
| **Intrinsic** | `species.action_ids` — a list on the species row. Always present, never dependent on authored content, so a basic attack exists before any container does |
| **Granted** | `rpg_actor_action(owner_kind, owner_key, action_id, source)` — mirroring `effect_binding`'s owner vocabulary and its `source` column so a withdraw removes exactly what a grant added |

Resolution is intrinsic ∪ granted, deduplicated by `action_id`, ordered by `action_id` ordinal. Deterministic ordering matters here as much as in targeting: this list is what `A7` iterates.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~ActionModel"
dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~Action"
.\scripts\guard-dal.ps1
```

## Structure

```
src/FusionRpg.Core/Actions/ActionRow.cs          (the record, closed enums, tag set)
src/FusionRpg.Core/Actions/ActionCostRow.cs      (cost + when)
src/FusionRpg.Core/Actions/ActionEffectScope.cs  (scope enum + resolution)
src/FusionRpg.Core/Actions/ActionRowValidator.cs (reject, never coerce)
src/FusionRpg.Data/Sqlite/RpgStore.Actions.cs    (all SQL, per guard-dal)
tests/FusionRpg.Core.Tests/Actions/
```

## Testing strategy

**The corpus is the test.** Each case is either expressible in the schema or explicitly excluded by it, and this module is where that is demonstrated rather than discovered mid-build:

| Case | Proves |
|---|---|
| Basic attack | The whole chain at its simplest — and must stay **byte-identical** (`A5`) |
| Strike + self-heal | Two scopes in one action, and §4's ordering rule |
| Three-hit combo | `resolve_offsets_json` × `commitment` over a target **set** |
| Ranged with a minimum | `min_range` / `max_range`, **inert with no board** |
| Summon into a cell | Costs, `anchor_source`, free-cell placement, `spawn.entity` — the game's core verb |
| Drain-channel | `when = perTick`; running dry ends the action via the interrupt path |

Plus, and these are the ones that fail quietly if omitted:

- **Validation rejects, never coerces** — unknown `resource_id`, unknown `container_id`, `min_range > max_range`, an unknown tag, a scope naming an atom the container does not hold. Each is a planted-violation test, so the validator is proven able to fail.
- **A no-board range check passes** — asserted directly, because the byte-identity of `A5` rests on it.
- **Cost rollback is atomic** — an action whose second cost fails leaves the actor's pools exactly as found, asserted per pool rather than in aggregate.
- **A `ValueSpec` in an action reads the same curve as one in an atom** — one scaling mechanism, proven by shared fixture rather than by inspection.

## Boundaries

- **Always:** reuse `ValueSpec`, `TargetSpec`/`TargetResolver`, `DamageApplyPipeline`, and `effect_binding` rather than adding a parallel one; keep all SQL inside `FusionRpg.Data`; reject unknown ids loudly.
- **Ask first:** anything that changes a sealed atom contract — §4's ordering resolution is the live example; adding a `ContainerKind`; adding a predicate leaf (that is `E3`'s closed list, and `A4` contributes to it rather than forking it).
- **Never:** a second targeting system, a second condition language, a second scaling mechanism, or a second binding concept. Never put action runtime under `Core/Battle/Timeline/`. Never let an action be authored against the PvZ channel — actions are `rpg.*`, and the two games share no state.

## Success criteria

1. An action exists as **rows**, not as a C# catalog — the fifth-content-system problem does not recur one layer up.
2. All six corpus cases are expressible, or excluded in writing with a reason.
3. Adding a new action costs **one row plus its costs and scopes** — no build, no code.
4. The basic attack, driven through this model, is byte-identical (proven in `A5`).
5. Nothing here invents targeting, damage application, effect resolution, or action timing — each calls the shipped owner.
