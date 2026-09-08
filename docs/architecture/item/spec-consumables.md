# Spec: `consumables`

**Module id:** `consumables` · **Program:** [item](../item-map.md) · **Build order:** 18 of 21
**Depends on:** `equip-assign` (4) · and `armoury` (2) for `rpg_item_stock`
**Rulings:** D5, D16, D26, **D27** (the container-kind conflict, §*Open*) · lane
[G2 `ssot-consumables.md`](ssot-consumables.md)

## Objective

G2's rule, and the whole module is one sentence of discipline around it:

> **Degenerate the use path, never the effect.** A v1 consumable is spent at a menu before a run, its
> effect lasts the run, and **its atoms are real from day one.**

That last clause is what makes the later absorption into the action layer *"one UPDATE on two nullable
columns and one INSERT"* (§4.1) rather than a migration. A `heal_amount INT` column would have to be
migrated, re-priced, re-displayed and re-hashed the moment an action could fire it.

**Users:** the dispatch endpoint and the squad builder (`ConsumableCatalog.GateManifest`), module 14
`salvage-craft` (recipes output consumables), module 20 `item-surfaces` (the manifest screen).

## Design

### ✅ `OnActivate` EXISTS — the lane's headline request is answered, with three deltas

`ssot-consumables.md` §4.2 / §9 item 1 is the lane's *"hardest finding"*: an instant consumable has no
trigger it may legally name, so the lane requests an eighth trigger, `OnUse`, as a named SC2 change.

**The eighth trigger shipped.** Verified:

| Fact | Evidence |
|---|---|
| There are **8** triggers, not 7 | `AtomKindRegistry.cs:22` — `public const int TriggerCount = 8;` |
| The eighth is `OnActivate` | `AtomKind.cs:71`, and `AtomTriggers.All` at `:74` |
| It is a **third category**, neither board event nor grant lifecycle | `AtomKind.cs:88-93` — *"An actor's own decision to act, independent of any board event or grant lifecycle"* |
| It reached the vocabulary as a **reviewed cross-program change**, not a unilateral one | `AtomKind.cs:58-60` — *"added A18b (spec-on-activate-trigger.md) — a cross-program vocabulary change, reviewed via that spec"* |
| Four kinds carry it | `stat.modify` (`:218`), `resource.delta` (`:291`), `status.apply` (`:345`), `shield.grant` (`:397`) — each takes `AllTriggers` (`:29-31`) |

**Three deltas from what the lane asked for, and the third is the interesting one:**

| Lane's ask | Shipped | Effect here |
|---|---|---|
| Call it `OnUse` | **`OnActivate`** | Consumables **name `OnActivate`**. There is no `OnUse` and there must not be a second name for one concept |
| Allow it on the kinds reachable from a grant | Exactly four, listed above | The six board kinds and `resource.economy` / `status.clear` stay on `AtomTriggers.Events` — deliberately (`AtomKindRegistry.cs:25-28`, H3) |
| ⛔ **Forbid** it on `stat.modify` | **Allowed** on `stat.modify`, with `TriggerOptional: true` | The lane's *reason* for forbidding it is preserved by a better mechanism — see below |

⭐ **The `TriggerOptional` correction, because the lane's objection was right and its remedy was not.**
The lane wanted `stat.modify` excluded so *"no trigger"* would keep its one meaning: *permanent
modifier, never expires* (definitions §14.2). Shipped code keeps that invariant a different way —
`AtomKind.TriggerOptional` (`AtomKind.cs:132-139`) adds a third case to a binary that had only
*"triggers forbidden"* and *"a trigger is required"*, so `stat.modify` is the one kind that carries both
shapes at once and the permanent case is **completely unchanged**. `AtomRowValidator.ValidateWhen`'s
`Count > 0 ⇒ required` inference was the thing that broke, and it was found *by running an existing
fixture*, not by reading. So:

> **The v1 fallback (§4.2 option b — a triggerless `resource.delta` under a `consumable` container) is
> dead, and should not be carried forward.** It existed only because option (a) might be refused. It
> was not.

### ⛔ D6 is closed for battle too — and the ruling stands while its stated reason does not

`ssot-consumables.md` §2.3 and §4.1(b) reject in-combat use with one argument: *"in battle a bound
`resource.delta` is a silent no-op … Battle's sink does handle FA10, but no ATOM can reach it."*

**That is no longer true.** `resource.delta` is `Lawn = Full, Battle = Full, Sim = PlanOnly`
(`AtomKindRegistry.cs:290`), and the comment directly above it (`:284-289`) records why:

> *"D6: Battle was Full, then downgraded to None because no ATOM could reach it — `BattleEngine` never
> granted and never called `OnEvent`. A18c (spec-battle-resource-shield-grants.md) grew that grant
> path: `OnActivate`/`OnDamageDealt` now fire, `Bag.Status`/`Bag.StatusRng` are wired, and a real
> shipped def (`fx.overlay_damage`) proves plain amounts, the DoT/contagion payload, and the
> owner-matching dual-fire all work end to end (`BattleResourceShieldGrantsTests`, T46). **Full again,
> for real this time.**"*

**The v1 shape does not change. Its justification does.** Battle-mode use is still out of v1, but the
reason is now narrow and nameable rather than a runtime wall:

| Blocker | State |
|---|---|
| Battle cannot execute a heal | ⛔ **Retired.** `resource.delta` Battle = Full |
| Battle has no **use affordance** — an in-combat surface where a player spends an item | Real. Module 20's, and out of its v1 |
| ⛔ **`A3`'s cost model has no shape for spending an item.** `rpg_action_cost` is `(action_id, resource_id, amount_spec_json, when_paid)` (`RpgStore.Actions.cs:65-71`), priced against the five actor resources. **A consumable's cost is an item, which is not a resource** | Real, and it is the lane's own §9.5(b) — unanswered from the action side |

So the ruling is: **ship (c), out of combat**, and stop citing D6 for it. An out-of-date reason attached
to a correct decision is how a decision gets reopened for the wrong cause.

### ✅ The usability leaf also shipped — §9 item 5(c) is answered

The lane asks A4 for *"a leaf that reads: do I hold ≥ 1 of this stock row"*, noting the leaf list is
closed. **`LeafId.HoldsStock` exists** (`PredicateNode.cs:29`), approved 2026-08-27 and landed
2026-08-28 under explicit cross-program authorization (`PredicateNode.cs:7-15`,
`CrossProgramLandedFlags.HoldsStockLanded = true`).

And the lawn half is closed from the action side as well: `ActionBindMode` is a closed two-member enum
(`ActionBindMode.cs:16-19`) and `ActionCompiler` **refuses a consumable action in lawn mode** —
`if (mode == ActionBindMode.Lawn && ContainsHoldsStock(tree)) return ActionRejection.Fail(
ActionRejectionReason.ConsumableUnsupportedInMode, …)` (`ActionCompiler.cs:97-98`).

⚠ **But the leaf reads caller-supplied quantities, not a store.** Its own comment says so:
*"The underlying inventory/stock SYSTEM (`rpg_item_stock`, item/ssot-consumables.md) is unbuilt —
confirmed absent by search, not assumed"* (`PredicateNode.cs:10-12`). Verified: `rpg_item_stock` appears
in exactly two `src/` files and both are comments saying it does not exist.

> **So this module's real upstream is module 2 `armoury`, not the action program.** `rpg_item_stock` is
> module 2's table; `holdsStock` becomes answerable the moment it exists, with no change to the leaf.

### ⛔ The atom layer still has no binding with a lifetime — verified, and carried

`ssot-consumables.md` §4.5 checked this in code and concluded a timed buff must be a **status**. It is
still true.

```sql
-- RpgStore.AtomInstances.cs:83-92
CREATE TABLE IF NOT EXISTS effect_binding (
  binding_id TEXT NOT NULL PRIMARY KEY,
  instance_id TEXT NOT NULL,
  owner_kind TEXT NOT NULL, owner_key TEXT NOT NULL DEFAULT '',
  slot TEXT, priority INTEGER NOT NULL DEFAULT 0,
  source TEXT NOT NULL DEFAULT '', bound_utc TEXT NOT NULL,
  revision INTEGER NOT NULL DEFAULT 0
);
```

**No expiry, no duration, no until-tick — and no foreign key either** (that absence is S2, module 1's).
Withdrawal stays explicit. So:

> **A timed buff is a status. A run-scoped buff is a lifecycle. v1 uses the second, because it needs
> nothing new.** Bind at run start at `player:{id}` with `source = 'draught'`, withdraw at run end by
> `source` — the index for which already exists (`RpgStore.AtomInstances.cs:97`).

⚠ **One lane claim has drifted and the drift does not help.** §4.5 calls `StatusPayloadKind.ModifyStat`
*"declared and dead … four references, all in the file that declares them."* It now has two production
declarers — `ExhaustionPolicy.cs:77` and `StanceRuntime.cs:46`, both in the action program. **But the
mechanism the lane actually asked for is still absent:** `StatusDef` is
`(StatusId, Kind, Family, Categories, Tags, Stacking, PayloadKinds, Element?, …)`
(`ResistanceEvaluator.cs:33-48`) and **carries no container reference**. *A status whose effect is a
container of atoms* does not exist. The locked Resource model still requires the identical thing for
exhaustion (`decisions.md`, *Resource model (2026-08-22)*), so the joint ask in §9 item 3 stands
unchanged — it is just no longer true that nothing reads the enum.

### ⛔ Open, and it is not this module's to resolve: the container kind

The lane needs a fifth `container_kind` (`consumable`, prefix `consumable.`, `slot IS NULL`,
zero rolls) and argues it out properly against reusing `item` (§4.6). **`ContainerKind` still ships six
values** — `Item · Trait · Skill · SpeciesPassive · Patron · WorldBuff` (`ContainerRow.cs:7-14`), with
`PrefixOf` at `:141-150`.

⛔ **And D27 does not include it.** D27 mints exactly four — `gem` · `set` · `charm` · `combo` — one per
mechanism, *"checked for collision against rarity rungs, the 15 slot roles, plant slot names and the
power classes"* (item-ideal §2f.3). **`consumable` is a fifth ask that no ruling covers.**

| | |
|---|---|
| **The conflict** | the lane says five; D27 says four and does not name this one |
| **Why it cannot be assumed** | `ContainerKind` is closed by comment — *"adding one is a reviewed change, because each implies a spec that owns its authoring and its lifecycle"* (`ContainerRow.cs:4-5`) — and `definitions.md` §1 requires the `container_id` prefix to match |
| **Decider** | **the owner**, through effect-atom, on the same amendment that lands D27's four. Batch it: one review of five values costs what one review of four costs |
| **Fallback if refused** | reuse `item` with `slot IS NULL`. The lane already refused this and its reason is good — every `item_base_type` field becomes *"NULL means consumable"*, a discriminator by absence that leaks into every query that forgets to check. Recorded so the fallback is a decision, not a drift |

**This module does not proceed past its DDL until that is answered.** Everything above it — the atoms,
the recipes, the manifest gate — is independent of which value the enum ends up carrying.

### Data shape

Two new tables. Everything else is reused.

**`consumable_def`** — 1:1 on the container, nine columns, exactly as §5.2 specifies. The two that
matter here:

| Column | Notes |
|---|---|
| `class_id` | closed enum, six values: `restore` `draught` `ward` `board` `revive` `utility`. **v1 authors three** (`restore`, `draught`, `ward`); the other three are declared and ungenerated — the same disposition D14 gave `standard` and seedsmith gave `environment` |
| `use_context` | closed comma-joined set `menu · dispatch · battle · lawn`. **v1 authors `menu` and `dispatch`.** Widening is additive and never invalidates a row, which is the whole no-migration proof |
| `grants_action_id`, `cooldown_key` | the seam, both nullable and inert in v1. `cooldown_key` is authored now because a cooldown *group* is not retrofittable after content ships |

**`rpg_run_draught`** — PK `(run_kind, run_id, seq)`. **A determinism input, not a log.**
`ExpeditionResolver` is pure over `(tier, squad, seed, elapsedTicks)` (`ExpeditionResolver.cs:39-49`), so
a draught must be part of the sealed input and needs a stable row order. Written in the same transaction
that decrements the stack; **recall refunds nothing**, or dispatch-and-recall is a free outcome preview.

### The spam defence is structural, and it is not a clock

| Defence | Standing | Why not a cooldown |
|---|---|---|
| Carry limit `N` (proposed **2**, the owner's number) | **Primary.** `DraughtLimitExceeded` refuses the dispatch, and the manifest is an input to the sealed run so there is no path around it | — |
| One per `exclusion_group`, defaulting to `(family_id, variant)` | **Secondary.** The shipped pool-group rule (`ContainerRow.cs:31-35`), reused rather than reinvented | — |
| A competing material sink | Tertiary | — |
| A cooldown | **Refused for v1** | *"A cooldown guards a door. v1 has no door."* And there are already two mechanisms — `icd_key` at the atom layer and `rpg_action.cooldown_key`, which **now exists** (`RpgStore.Actions.cs:48`). A third would be a third |

⚠ **And a live runtime property that must be validated, not documented:** `EffectBag.FireGrant`
short-circuits both `PassesOverlayFilters` and `_proc.TryPass` on the lifecycle path, so an atom that
fires through it gets **no chance roll and no ICD** whatever it authors. **A consumable may never author
`chance` or `icd_ms`** — refused at import with `ParamNotHonoured`.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Consumable"
dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~RunDraught"
```

## Project structure

```text
src/FusionRpg.Core/Items/Consumables/ConsumableDef.cs        new — the row, the two closed enums
src/FusionRpg.Core/Items/Consumables/ConsumableCatalog.cs    new — SC7's named consumer:
                                                               Resolve(containerId), GateManifest(...)
src/FusionRpg.Core/Items/Consumables/DraughtProjection.cs    new — manifest -> BattleChannelMod, the
                                                               same transform ApplyInjuries runs with
                                                               the opposite sign
src/FusionRpg.Data/Sqlite/RpgStore.Consumables.cs            new — consumable_def, rpg_run_draught
src/FusionRpg.Core/Effects/Atoms/ContainerRow.cs             edit — ContainerKind.Consumable (ASK FIRST)
tests/FusionRpg.Core.Tests/Items/ConsumableTests.cs          new
tests/FusionRpg.Core.Tests/Items/DraughtManifestTests.cs     new
```

## Code style

```csharp
// OnActivate, not OnUse: the eighth trigger shipped under A18b (AtomKind.cs:71) and there must not be
// a second name for one concept. It is legal on exactly four kinds -- the ones whose executor is
// reachable from a grant -- so the check asks the registry rather than carrying a list that can drift.
static AtomRejection ValidateConsumableTrigger(AtomRow atom)
{
    if (atom.When?.Trigger is not { } trigger) return AtomRejection.Ok;   // permanent modifier: fine
    var kind = AtomKindRegistry.Get(atom.KindId);
    return kind is null
        ? AtomRejection.Fail(AtomRejectionReason.UnknownKind, atom.KindId)
        : kind.AllowsTrigger(trigger)
            ? AtomRejection.Ok
            : AtomRejection.Fail(AtomRejectionReason.TriggerNotAllowed,
                $"{atom.AtomId}: {atom.KindId} does not carry '{trigger}'");
}
```

## Testing strategy

| Test | Asserts |
|---|---|
| `a_consumable_names_OnActivate_and_validates` | the shipped eighth trigger, over a real `resource.delta` atom |
| `OnUse_is_not_a_trigger_and_never_becomes_one` | one name per concept — `AtomTriggers.IsKnown("OnUse")` is false |
| `OnActivate_is_legal_on_exactly_four_kinds` | `stat.modify`, `resource.delta`, `status.apply`, `shield.grant` — read from the registry, not from a copied list |
| `a_permanent_stat_modify_with_no_trigger_still_validates` | `TriggerOptional` — the §14.2 invariant the lane wanted protected, protected by the shipped mechanism |
| `resource_delta_is_battle_full_so_the_v1_reason_is_the_use_site_not_the_runtime` | `AtomKindRegistry.cs:290`, asserted so the retired justification cannot be re-cited |
| `a_consumable_authoring_chance_or_icd_ms_is_refused` | `ParamNotHonoured` — the lifecycle path honours neither |
| `a_consumable_container_with_any_roll_is_refused` | `PrefixRolls`/`SuffixRolls` both 0, `rarity` NULL, no tier window |
| `every_consumable_container_has_a_def_row_and_the_reverse` | an orphan container is not content |
| `grade_equals_the_tier_of_every_core_atom` | I3's band-consistency rule, borrowed |
| `every_core_atom_is_legal_in_every_runtime_the_use_context_names` | the invisible-nerf guard (failure mode 5), at **catalog load** |
| `the_manifest_gate_refuses_above_N` | `DraughtLimitExceeded`, at the dispatch gate, not after |
| `two_manifest_entries_sharing_an_exclusion_group_are_refused` | `DraughtFamilyConflict` |
| `a_draught_is_spent_in_the_same_transaction_as_the_stock_decrement` | failure mode 7 — no peek-and-refund |
| `recall_refunds_no_draught` | the same exploit from the other side |
| `run_draughts_are_written_before_the_seed_resolves` | determinism input, not a log |
| `a_run_scoped_draught_is_withdrawn_by_source_at_run_end` | one snapshot mechanism shared with charms, keyed on `source` |
| `no_binding_carries_a_duration` | `effect_binding`'s columns, as a schema assertion — the reason a timed buff must be a status |
| `holdsStock_is_answerable_once_rpg_item_stock_exists` | the leaf shipped; this test is `Skip`-with-a-reason until module 2 lands, and the reason names the module |

## Boundaries

**Always:** author the effect as a container of atoms from day one; name **`OnActivate`**; spend at
dispatch inside the stock-decrement transaction; validate every core atom against **every** runtime the
`use_context` names, at catalog load; keep the run-start snapshot shared with charms (`source` is the
only difference).

### ⭐ D37 — the carry limit is a belt, not a constant

> **Owner, 2026-09-04:** *"add bag feature like diablo belt."*

§10.1 proposed **`N = 2`** and called it *"the single most consequential number here."* **It is not a
number — it is an item property**, and the slot to hang it on already ships.

| | |
|---|---|
| The slot | ⭐ **`girdle`** — role 7 of the fifteen, budget **60‰** (`spec-slot-roles.md:53`). **No sixteenth role is needed** |
| The property | a `girdle` base type carries `consumableSlots`; a better girdle carries more |
| Where it lives | the **base type** (module 6), so it is content, rolled and dropped like everything else — not a tunable |
| What replaces `N` | nothing. There is no global carry limit; **the equipped belt is the limit** |

**Why this is the better answer, in this program's own terms.** Everywhere else the rule is *a number a
balance pass would change belongs in `data/tuning/`*. A carry limit is different in kind: it is
something the **player should be able to improve by playing**, which makes it a content axis, not a
config row. A belt turns *"how many potions may I hold"* from a designer's constant into a drop worth
finding — which is D26's loop (find gear → play deeper → find better gear) applied to the one number the
lane was most worried about.

⚠ **Consequences to carry through:**

- Module 6 authors `girdle` base types with a `consumableSlots` value on the directional-profile pass
- ⛔ **`consumableSlots` is a magnitude a player grows — so it takes no hard ceiling** (`AGENTS.md`).
  A structural upper bound may exist for UI layout; if so it says so in a comment and **throws**
- With no belt equipped the count is **0**, not a default — an unequipped slot grants nothing, exactly
  as every other role behaves
- Module 20 renders the belt as its own strip, not as a row of the armoury list

**Ask first:** ⛔ **`ContainerKind.Consumable`** — a fifth value D27 does not cover; batch it with D27's
four. ~~**`N`, the carry limit** (§10.1) — proposed 2, and it is the single most consequential number
here. Whether consumables ever exist in PvZ mode (§10.2 — the lane recommends yes, later, via the intent
road, never the action queue). Per-squad vs per-specimen draughts (§10.4). A status whose payload is a
container of atoms — **ask it jointly with the Resource model**, which needs the identical mechanism.

**Never:** build a second scheduler — no timer, no cooldown, no queue in this module. Never author a
permanent stat-up as a consumable (§3.2 — no container to bind to, invisible to the power model,
duplicates enhancement). Never give a consumable a rarity rung. Never let `chance` or `icd_ms` onto one.
Never encode the effect as a scalar column — that is the one thing that would make the absorption a
migration.

## Success criteria

- [ ] A v1 consumable's effect is `effect_container_atom` rows and nothing else; no scalar effect column
      exists anywhere in the module.
- [ ] A consumable atom names **`OnActivate`** and validates against the shipped registry; the string
      `OnUse` appears nowhere in the tree.
- [ ] A permanent `stat.modify` with no trigger still validates — the §14.2 invariant is intact.
- [ ] `chance` / `icd_ms` on a consumable is refused at import, with the `EffectBag` short-circuit cited
      in the code comment.
- [ ] The manifest gate refuses above `N` and on a repeated exclusion group, **at dispatch**, with both
      refusals reaching the player as text.
- [ ] Draughts are spent in the decrement transaction and recall refunds none.
- [ ] `rpg_run_draught` rows are written before the seed resolves, and the expedition replays identically
      from them.
- [ ] Every core atom is legal in every runtime its `use_context` names, proven by a planted violation.
- [ ] The container-kind question is **answered by the owner before the DDL lands** — either
      `ContainerKind.Consumable` or the documented `item` fallback, never drifted into.
- [ ] The lane's §2.3 / §4.1(b) D6 citation is corrected in `ssot-consumables.md` with
      `AtomKindRegistry.cs:290` cited, and the ruling restated on its real blocker.
