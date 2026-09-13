# Spec: Item upgrade tree (`item-upgrade-tree`)

**Initiative:** `species-gear-chain` ([map](../species-gear-chain-map.md)) · **Module:** `item-upgrade-tree`
**Owning program:** `item`
**Depends on:** `rarity-promotion`, `craft-risk-ladder`
**⛔ Blocked on external unbuilt work:** `item` module 23 `requirement-profiles` — **approved 2026-09-09, NOT BUILT** (`grep -rn "RequirementProfile" src/` returns **zero hits**)
**Status:** spec, 2026-09-13. Awaiting owner approval. No build authorized.
**Source ideal:** [gear-climb-ideal.md](../gear-climb-ideal.md) § The shape 2 (E5)

---

## Objective

**Let an item become a different, better item — consuming the old one, carrying its identity across.**

This is the one place in the whole initiative where the schema **genuinely cannot express the
request.** Promotion is in-place (`outputKind: mutation`, no `outputRef`), so it can never change what
an item *is*. E5 is a different verb: an item-typed cost line, a consume-and-replace `output_kind`,
and a successor edge.

---

## ⛔ The finding that changes this module's design

The ideal proposes the **class ladder** as the successor spine (`cloth→leather→scale→plate`) and notes
it *"already exists as ordering."* **Reading the registry shows the ordering exists for four ladders
and means four different things.** Quoted from `classes.v3.json`'s own `rungCountRationale` fields:

| Ladder | Rungs | What the ordering means | Usable as an upgrade spine? |
|---|---|---|---|
| **armour** | 4 | Weight: `cloth → leather → scale → plate` (tags `light` → `medium-light` → `medium-heavy` → `heavy`); plant frame `fibre → husk → bark → heartwood` | ✅ **Yes.** A genuine progression |
| **weapon** | 3 | ⛔ *"Rungs are ordered by **combat role, not raw damage number**, so the same rung means the same fighting style on both frames."* `blade` (light melee) → `blunt` (medium melee) → `launcher` (heavy **ranged**) | ❌ **No.** A style axis |
| **offhand** | 2 | ⛔ *"The off-hand's entire mechanical question is binary — **does it guard, or does it not**."* `focus` (no-guard) → `bulwark` (guarding) | ❌ **No.** A category switch |
| **jewel** | 3 | *"Ordered by **potency/commitment** (light claim → bold statement → binding pact), not by mass"* | ⚠ **Arguably.** Commitment is not power |
| **standard** | 2 | Commander gear, *"single-band in v1 — it is not item-level tiered"* | ❌ Out of scope |

⭐ **So "upgrade along the class ladder" is correct for armour and wrong for weapons.** Upgrading a
`blade` into a `launcher` would not make a better weapon — it would turn a fast melee weapon into a
heavy ranged one, silently changing what the player's build does. That is **D2's
upgrade-becomes-downgrade failure**, reached by a different route than requirement creep.

### ⚠ Correction, 2026-09-13 — the rationale strings do not prove this, and armour fails the same test

An earlier draft of this spec rested the armour/weapon split on the `rungCountRationale` strings
alone. **That reading is asymmetric and does not survive.** Armour's own rationale asserts *weight
class*, not power: *"Fewer than four collapses two of the nine armour-bearing roles' base types into
indistinguishable **weight classes**"* — and `cloth` and `plate` carry **identical `roles` arrays**.
On the test as stated, armour disqualifies itself exactly as weapon does.

**The evidence that actually vindicates armour is elsewhere, and it is decisive**
(`docs/architecture/item/ssot-item-categories.md`):

- `:493` — *"Bands rise ×2.1, **class rungs rise ×1.4**"*
- `:627-628` — *"band-3 cloth `core-protective` is `280‰ × 780 = 218 hp` (196–240) against plate's
  501. **Plate wins guard by 2.3× and never overlaps.**"*

**A strict, non-overlapping guard-magnitude ladder is a progression spine. The weapon ladder has no
such table, because it is not one.** The conclusion stands; the argument is replaced.

### ⛔ And the constraint that correction exposes — the one this module must actually satisfy

The same section states what makes the armour ladder survivable, and it is not the guard number:

> `ssot-item-categories.md:629-631` — *"Cloth's entire compensation therefore has to come from its
> **class-tagged affix pool** and its **implicit slate**. If I8 does not differentiate
> `armour-cloth` from `armour-plate`, cloth is strictly worse and the ladder collapses into 'wear the
> heaviest thing you can.'"*
>
> `:746-747` — *"I8 — the affix pool must be filtered by `affix_pool_tag`, and **cloth must not be
> strictly worse than plate**."*

⛔ **So "carry every affix across untouched" — this spec's own §2 rule and success criterion 2 —
launders `armour-cloth`-pool affixes onto a plate chassis**, producing a combination the drop path
can never roll. And because the successor is a different base type it carries a different
`implicitFamily` (`classes.v3.json` `implicitSlates`), so **the piece's implicit silently changes**.

That is D2's upgrade-becomes-downgrade by a third route — on the one ladder this spec certified as
safe. **Two rules are therefore mandatory, and they are Design §2a below.**

### And a correction to the ideal's cost estimate

The ideal states the class ladder *"is currently read by no C# code, so adopting it is itself work."*
**That is not accurate.** It is read by:

- `tools/ItemSeedValidator/Registries/RegistrySet.cs:371` — iterates `classLadders`
- `tools/ItemSeedValidator/Checks/ReferenceCheck.cs:130` — validates `class` references against it
- `seedsmith/adapters/items/basetypegen/tuning.py:96` and `registries.py:79`

**The precise statement is: it is read by the validator and the generators, not by the runtime
(`FusionRpg.Core`).** Every entry already carries an explicit `rung` integer, so a successor edge is
**expressible today** — `rung n → rung n+1` within `(ladder, frame)`. That is *less* work than the
ideal estimated, on the armour ladder where it is meaningful.

⚠ **But the registries are `frozen: true` with `minCompatibleVersion`**, and there are three files
(`classes.v1/v2/v3.json`, at `registryVersion` 3/4/5). Adding successor edges is a **reviewed registry
change**. ⚠ **Corrected:** an earlier draft cited `v4Note` as the additivity precedent. It is not — `v4Note` records *"re-derive the 32-family global exclusion list against `AtomKindRegistry.cs` instead of the frozen v1 designNotes snapshot"*, a **replacement** of a frozen snapshot. The additivity constraint is real but rests on `minCompatibleVersion` and `stage1aFrozen` (*"a change to this registry invalidates authored content unless minCompatibleVersion says otherwise"*), which is what this spec must argue from.

---

## What exists today — verified

### Built

- **`CreatureRecipeDef`** (`Creatures/Fusion/CreatureRecipeCatalog.cs:11-13`) — full signature
  `(string RecipeId, string OutputSpeciesId, string InputSpeciesIdA, string InputSpeciesIdB, bool CrossRungGapFill = false)`. The creature side already ships a
  consume-two-produce-one shape. It is the structural precedent, on the other side of the house.
- Every class ladder entry carries `id`, `frame`, **`rung`**, `nameKey`, `identity`, `roles`, `tags`.
- `ItemWorkbench`'s six-verb pattern, the mutation ledger, and the recipe corpus (67 recipes,
  `outputKind` / `outputQty` / `costLines` / `soulsCostBand`).
- ~~`requirement-profiles` (item module 23) exists~~ ⛔ **STRUCK — it does not.** `grep -rn "RequirementProfile" src/` returns **zero hits**; there is no `requirement-profiles` tuning file. What exists is `docs/architecture/item/spec-requirement-profiles.md` and `item-map.md`'s module 23 row, *"approved 2026-09-09"*, **build order 23 -> 24 -> 25, none built**. The only shipped requirement surface is presentation-only (`Items/Display/ItemCard.cs:452-481`). **A doc saying "approved" is not evidence something is built** — moved to Real gap.

### Real gap

| Gap | What must be built |
|---|---|
| Item-typed cost line | `costLines` today names `material` ids; consuming an **item instance** is a new line kind |
| Consume-and-replace `output_kind` | Every shipped recipe is `mutation` (in-place) or a mint. Neither consumes an instance and produces a different one |
| Successor edge | Expressible via `rung`, but not declared — and only meaningful on one ladder |
| An `op_kind` member | `MutationOpKind` is closed and ask-first. ⚠ This is the **third** queued ask on module 15, behind `Repair` and `Elevate` |

---

## Design

### 1. Ship armour first, and say so

**Scope v1 to the armour ladder** (both frames: `cloth→leather→scale→plate`,
`fibre→husk→bark→heartwood`). It is the only ladder whose ordering is a progression, and it covers
**nine of the fifteen roles**.

Weapons, offhands and jewels need a **successor field that is not the class ladder** — see Open
question 1. Shipping armour proves the whole mechanism (cost line, output kind, affix carry,
requirement check) against a spine that is genuinely correct.

### 2. ⛔ Do not reroll on upgrade

PoE's Blessing orbs reroll everything and **that is the documented complaint.** Affixes carry across
unrerolled, exactly as promotion does — **subject to §2a, which is not optional.**

### 2a. ⛔ Affix-pool legality and the implicit swap — the two rules the guard ladder demands

**Rule 1 — the successor's affix set must be legal on the successor's own `affix_pool_tag`.**
Carrying an `armour-cloth`-pool affix onto a plate chassis creates an item the drop path cannot roll
and removes cloth's only compensation (`ssot-item-categories.md:629-631`, `:746-747`). An upgrade
whose affix set is not legal on the successor **refuses**; it does not silently relabel.

⚠ **This makes the upgrade lossy or refusing, and that is a real design choice, not a detail.** Three
shapes exist and only the first is safe by default:

| Shape | Verdict |
|---|---|
| **Refuse** when any affix is illegal on the successor's pool | ✅ **v1.** Consumes nothing, explains itself, and cannot produce an unrollable item |
| Drop the illegal affixes | ❌ Silent value loss on a consumed input |
| Re-tier them into the successor's pool | ❌ That is a reroll, banned by §2 |

**Rule 2 — the implicit change is presented before the input is consumed.** The successor's
`implicitFamily` comes from its own row in `classes.v3.json` `implicitSlates` and will usually differ.
The card must show the outgoing and incoming implicit, under the **same refuse-never-warn posture**
this spec already applies to requirements — the input is consumed and there is no undo.

### 3. ⛔ Do not claim to be the named successor

D2 had to put a wiki warning on this. An upgraded `cloth` piece becomes a **`leather` chassis carrying
its own identity** — it is not "a Leather Vest." **The card must say so**, and this is a requirement,
not a nicety.

### 4. ⛔ Requirement creep is the live risk

⛔ `requirement-profiles` is **unbuilt** (above), so this rule is specced and **cannot be built until module 23 ships**. **An upgrade that raises a requirement past what the owner can meet is
a downgrade wearing a better name.** The executor must check the resulting requirement against the
owner **before** consuming the input, and refuse with a message naming the unmet requirement.

⭐ **Refuse, never warn-and-proceed.** The input is consumed; there is no undo.

### 5. Risk comes from the ladder

⛔ **Upgrade has no failure chance of its own.** Risk is a property of the item, in
`craft-risk-ladder`. The input is consumed on success; a refusal consumes nothing.

---

## Tech stack

C# .NET 8 (`FusionRpg.Core/Items`, `FusionRpg.Server`, `FusionRpg.Data`), xUnit. No new dependency.

## Commands

```powershell
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Upgrade"
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Recipe"
dotnet test tests/FusionRpg.Data.Tests --filter "FullyQualifiedName~InstanceOp"
dotnet run --project tools/ItemSeedValidator
dotnet test tests/FusionRpg.Server.Tests
.\scripts\guard-dal.ps1
python scripts/audit-overflow.py
```

## Project structure

| Path | Role |
|---|---|
| `data/seed/items/_registry/classes.v3.json` | Successor edges — a **reviewed, additive** registry change |
| `src/FusionRpg.Core/Items/Mutation/MutationOp.cs` | The new `op_kind` member |
| `src/FusionRpg.Core/Items/…/UpgradePolicy.cs` | Pure resolve, requirement check |
| `src/FusionRpg.Server/ItemWorkbench.cs` | The **eighth** verb (six ship today: `Salvage` :164, `Upcycle` :194, `Enhance` :240, `SocketAdd` :293, `SocketInsert` :338, `SocketImbue` :378; `rarity-promotion` adds the seventh) |
| `src/FusionRpg.Core/Items/Materials/CostClassMatrix.cs` | ⛔ The **eleventh `CraftOperation`** member — closed, ask-first |
| `data/seed/items/recipes/recipes.json` | **Generated** — regenerated via its generator |

## Code style

```csharp
// Check the RESULTING requirement against the owner BEFORE consuming the input. requirement-profiles
// (module 23) exists, and D2's documented failure is an upgrade that raises a requirement past what
// the owner can meet — a downgrade wearing a better name. REFUSE, never warn-and-proceed: the input
// is consumed and there is no undo.
if (!RequirementProfile.Met(owner, successor))
    return Refusal.RequirementUnmet(successor.Id, RequirementProfile.FirstUnmet(owner, successor));
```

---

## Tunables

| Number | Meaning | Owner |
|---|---|---|
| `upgradeCostSoulsMilli` per class rung | E5's price, same shape as `elevate` | `data/tuning/materials.v1.json` `operations`, new verb |
| Class-ladder successor edges | Which chassis upgrades into which | `data/seed/items/_registry/classes.v{n}.json` — ⚠ **authored content, not a formula**, and a reviewed additive registry change |

⚠ **`upgradeCostSoulsMilli` prices on a rung, so it is a cost ladder and owes a
`ssot-power-scale.md` §10 row** — the same ask `rarity-promotion` files.

⛔ **No hard progression ceiling.** A configurable soft cap, never a hard stop.

**Structural (stays `const`, with a comment saying why):** the **no-reroll** rule and the
**consume-exactly-one-input** rule. They are the contract, not balance.

## Numeric types

- Costs are **`long`** and price on a rung, so they scale with the ladder.
- **Widen before multiplying; divide by 1000 last, exactly once; overflow throws, never wraps.**
- **Never `float`** — integer-exactness fails at `Θ` = 232, inside normal play.
- Class `rung` is a small identity `int` — an ordering position, **never a magnitude and never a
  multiplier.** An upgraded item is not a scaled item; its magnitudes come from its new base type
  through the paths that already exist.

## ActorHub gate

**Contributes nothing; consumes nothing.** An upgraded item grants stats through the existing
equipment path into `ActorHub` — it is a different base type wearing carried-over affixes, and both
halves already compose today.

⛔ **No `*Composer*`, no private fold.** `guard-actor-hub.ps1` stays green.

⚠ **One real consequence:** the upgraded instance is a **different instance**, so any Hub binding held
against the old one must be **withdrawn**. The withdraw-on-absence machinery already exists
(`RpgStore.Items.cs:846-859`) — use it; do not write a second one.

## Testing strategy

**Store tests run in memory**; a failed temp-delete is a **failure**, never `catch { }`.

| Level | What it asserts |
|---|---|
| Unit | ⭐ **Affixes carry across identically** — identity, tier and value, for every affix |
| Unit | The input instance is **consumed**; exactly one output exists |
| Unit | ⭐ **A refusal consumes nothing** — the input survives an unmet requirement, byte-identical |
| Unit | An upgrade raising a requirement past the owner's **refuses**, naming the unmet requirement |
| Unit | The successor is `rung n+1` within the **same (ladder, frame)** — never across frames |
| Unit | Top-rung upgrade refuses cleanly |
| Unit | ⭐ **Non-armour ladders are refused in v1**, explicitly, with a message saying why — so the weapon-ladder trap cannot be opened accidentally |
| Unit | Hub bindings against the consumed instance are **withdrawn** |
| Unit | Upgrade has no private failure chance |
| Unit | `checked` throws rather than wrapping |
| Integration | Full round trip: upgrade, re-equip, verify stats compose from the new base type |
| Contract | `ItemSeedValidator` green; every successor edge resolves; the registry change is **purely additive** (every id legal before is legal after) |

⛔ **No test asserts a recipe or item count.** Readings.

## Boundaries

**Always**
- Check requirements **before** consuming.
- Carry affixes across untouched.
- Make the card say the item is a *chassis carrying its own identity*, not the named successor.
- Withdraw Hub bindings against the consumed instance using the existing machinery.
- Keep registry changes purely additive, argued from `minCompatibleVersion` / `stage1aFrozen` (**not** from `v4Note`).
- Commit via MCP `repo-git.commit` with explicit `paths`.

**Ask first**
- ⛔ **The `MutationOpKind` member** — closed, ask-first, and **third in the queue** behind `Repair`
  and `Elevate`.
- ⛔ **Any change to `classes.v*.json`.** They are `frozen: true` with `minCompatibleVersion`, and
  `stage1aFrozen` records that *"a change to this registry invalidates authored content unless
  minCompatibleVersion says otherwise."*
- Extending beyond the armour ladder (Open question 1).
- The §10 row for the cost ladder.

**Never**
- ⛔ **Reroll on upgrade.** PoE's documented complaint.
- ⛔ **Upgrade along the weapon or offhand ladder in v1.** Those orderings are style and category
  axes. ⚠ **An earlier draft said "This is not a scope cut - it is a correctness requirement." That sentence is struck:** no owner decision scopes this to armour, and `gear-climb-ideal.md:277-278` says the opposite. Armour-only is a **scope choice justified by evidence** (the x1.4 / 2.3x non-overlapping guard ladder), and the correctness requirement is **Design §2a**, which binds on every ladder including armour.
- ⛔ Consume the input before the requirement check passes.
- Claim the upgraded item is the named successor item.
- Give upgrade a private failure chance.
- Make class rung a magnitude multiplier.
- Add a hard ceiling to the cost curve.
- Assert a population count.

## Success criteria

1. An armour piece upgrades to the next rung within its own `(ladder, frame)`, consuming the input.
2. Every affix carries across identically.
3. A requirement the owner cannot meet **refuses, and consumes nothing** — proven byte-identically.
4. The card presents the result as a chassis carrying its own identity, not as the named successor.
5. ⭐ Weapon, offhand and standard ladders are **explicitly refused** with a message naming the reason.
6. Hub bindings against the consumed instance are withdrawn via the existing machinery.
7. The registry change is purely additive; `minCompatibleVersion` semantics hold; `ItemSeedValidator`
   green.
8. No reroll, no private failure chance, no hard ceiling.
9. `guard-dal.ps1`, `guard-actor-hub.ps1` green; Core, Data and Server suites green.

## Open questions

1. ⭐ **What is the successor spine for weapons, offhands and jewels?** The class ladder is a style
   axis there, not a progression. **Recommendation: a separate, explicit `successorOf` field on the
   base type**, authored per chassis, rather than derived from any ladder — it is the only shape that
   expresses "a better blade" without expressing "a blade becomes a launcher." That is its own
   content pass and should not block armour.
2. **Does the upgraded item keep its `promoted_from_ordinal`?** It is a different base type but the
   same player's item. **Recommendation: keep it** — the mark answers *"was this natural?"* about the
   rarity, which the upgrade does not change.
3. **Does upgrade consume crafting potential, and does the successor inherit the remainder?**
   **Recommendation: consume potential, and derive the successor's afresh from its own base type**,
   with the *used fraction* carried across — otherwise upgrading becomes a potential-reset loop, which
   is the exact escape hatch the risk ladder exists to close.
4. **Does the species binding survive an upgrade?** A set piece's `speciesId` is on the set entry, not
   the instance — so a chassis change may leave the set. **Recommendation: refuse to upgrade a set
   piece in v1**, and record it, rather than silently breaking a set bonus.
