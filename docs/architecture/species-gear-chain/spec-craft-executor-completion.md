# Spec: Craft executor completion (`craft-executor-completion`)

**Initiative:** `species-gear-chain` ([map](../species-gear-chain-map.md)) · **Module:** `craft-executor-completion`
**Owning program:** `item` module 14 (`salvage-craft`) + module 16 (`sockets`), by verb ownership
**Depends on:** nothing that blocks it. **Runs beside `rarity-promotion`, not after it** — see
§"Relationship to `rarity-promotion`", which corrects the sibling spec's sequencing assumption.
**Status:** spec, 2026-09-13. Awaiting owner approval. No build authorized.
**Source ideal:** [tier-system-ideal.md](../tier-system-ideal.md):541-544 and :560-562

---

## Objective

**Make the priced, authored, served craft recipes that no POST can spend actually spendable — and keep
that defect separate from its mirror image.**

The workbench serves a recipe list from `GET /api/items/workbench/recipes`
(`src/FusionRpg.Server/WorkbenchEndpoints.cs:73`) that includes rows for verbs with **no executor at
all**. A player can read the price of a forge and can never perform one.

Measured this session against `data/seed/items/recipes/recipes.json` — **67 recipes**, by operation:

| Operation | Recipes | Executor today | Verdict |
|---|---|---|---|
| `temper` | 35 | `Enhance` (`ItemWorkbench.cs:240`, `TryRecipe(…, CraftOperation.Temper, …)` `:248`) | spendable |
| `elevate` | 10 | none | **stranded — `rarity-promotion`'s, not this module's** |
| `forge` | 7 | none | **stranded — this module** |
| `reroll-one` | 5 | none | **stranded — this module** |
| `upcycle` | 4 | `Upcycle` (`:194`, `:198`) | spendable |
| `bore` | 3 | `SocketAdd` (`:293`, `:300`) | spendable |
| `reroll-all` | 2 | none | **stranded — this module** |
| `socket` | 1 | `SocketInsert` (`:338`, `:347`) | spendable |
| `imbue` | **0** | `SocketImbue` (`:378`, `:386`) | **content gap — routeable, nothing to route** |
| `forge-gem` | **0** | none | **both gaps at once** |

**24 of 67 recipes are priced, loaded and served with no POST that can spend them** — the ideal's
count, independently re-derived and **correct**. Excluding `elevate`'s 10, **this module unblocks 14.**

---

## What exists today — verified against code and the shipped corpus this session

### Built

| Fact | Evidence |
|---|---|
| **`ItemWorkbench` exposes SIX public verb methods** — `Salvage`, `Upcycle`, `Enhance`, `SocketAdd`, `SocketInsert`, `SocketImbue` | `src/FusionRpg.Server/ItemWorkbench.cs:164`, `:194`, `:240`, `:293`, `:338`, `:378` |
| **Six matching POSTs**, plus the recipe list and the insert list | `WorkbenchEndpoints.cs:104`, `:111`, `:120`, `:133`, `:145`, `:161`; `:73`, `:89` |
| **`CraftOperation` is a closed ten-member enum** with kebab ids — *"the subset of [`op_kind`] that has a **price**"* | `Core/Items/Materials/CostClassMatrix.cs:11-44`, ids `:49-61` |
| **All ten operations are priced**, each with an `owner` field naming the module | `data/tuning/materials.v1.json` `operations` — `forge`/`upcycle` → *"salvage-craft (14)"*, `forge-gem`/`bore`/`imbue`/`socket` → *"sockets (16)"*, `elevate`/`temper`/`reroll-one`/`reroll-all` → *"enhance-reroll (15)"* |
| **The executor pattern is shipped five times over**: `Replay` → `TryResolve` → `TryRecipe(recipeId, CraftOperation.X, …)` → named seeded stream → policy → `_store.TrySpendAndApply` | `ItemWorkbench.cs:240-287` is the canonical read; `TryRecipe` itself at `:586` |
| ⭐ **Reroll's domain logic is already built and heavily tested** — `ValidateTargets` (`:68`), `ValidateRerollable` (`:106`), `TargetsFor` (`:138`), `RetainedGroups` (`:154`), `ValidatePostOp` (`:176`), `RungLegMilli` (`:218`), `CostMultMilli` (`:235`) | `Core/Items/Mutation/RerollPolicy.cs`; **36 references** from `tests/FusionRpg.Core.Tests/Items/RerollPolicyTests.cs`, plus production readers in `Instantiator.cs`, `RpgStore.InstanceOps.cs` and `RarityBudgetKeys.cs` |
| ⭐ **Reroll needs NO closed-enum amendment** — `MutationOpKind.RerollValue` and `RerollAffix` already exist, and §5.3 already reserves `reroll-value` / `reroll-affix` for lane I7 | `Core/Items/Mutation/MutationOp.cs:19`, `:22`; `item/ssot-enhancement.md` §5.3 |
| ⭐ **Forging an item instance is now possible.** `EquipmentContainerBuild.From` builds a container from a base type on the fly, and `LootMintAt.Mint`'s Equipment arm instantiates it | `Core/Items/EquipmentContainerBuild.cs:47`; `Core/Items/Drops/LootMintAt.cs:65`, Equipment arm `:76-88` |
| ⭐ **`InstanceOrigin.Craft` already exists** — the origin a forged item needs is minted, not missing | `Core/Effects/Atoms/Instantiator.cs:10` |
| **The no-instance spend shape is shipped** — `Upcycle` spends and grants with `grants:` and no mutation at all, which is exactly the shape `forge` needs | `ItemWorkbench.cs:206-217` |
| **The ledger is idempotent per `(instance_id, correlation_id)`**, and `Replay` short-circuits at every verb's first line | `RpgStore.InstanceOps.cs`; `ItemWorkbench.cs:243`, `:296`, `:343`, `:381` |
| **A recipe whose operation has no executor still validates, prices and loads** — which is exactly why the defect is invisible | `MaterialRecipeCatalog.cs`; `StrictLossLeak:282-284` even enforces a forge-specific invariant on rows nothing can run |

### Wiring gap — three verbs, 14 stranded recipes, no new vocabulary

1. ⛔ **`forge` — 7 recipes, no executor, no POST.** Every row is `outputKind: "container"` with an
   `outputRef` naming a base type (`recipe.001 → item.humanoid-torso-a-001`, …) and a
   `substrate` + `catalyst.forge` cost. Measured: `recipe.001-004`, `023`, `024`, `031`.
2. ⛔ **`reroll-one` — 5 recipes, no executor, no POST.** `recipe.015`, `016`, `026`, `027`, `028`;
   all `outputKind: "mutation"`, all riding `catalyst.flux` (plus an `essence.*` leg on four of them).
3. ⛔ **`reroll-all` — 2 recipes, no executor, no POST.** `recipe.017`, `018`; `shard.*` +
   `catalyst.flux`.

**All three are wiring, not design.** Reroll's policy, its op kinds and its cost math are built and
tested; forge's container assembly, instantiation and origin are built. What is missing in all three
cases is a method on `ItemWorkbench` and a `MapPost` beside it.

### Real gap — content, not code, and it is the inverse defect

⛔ **`imbue` is routeable with zero recipes.** `SocketImbue` (`:378`) resolves the target, then calls
`TryRecipe(recipeId, CraftOperation.Imbue, …)` (`:386`) against a corpus that authors **no `imbue`
row** — so it always refuses `material.recipe-unknown`. The code says so itself, in its own doc
comment: *"⏸ **Reachable but not yet payable:** the reference cost table prices `imbue` on `bore`'s
curve and the check runs at boot, but **no recipe row authors the verb** … the verb is wired, the
content is not, and the distinction is worth keeping visible"* (`ItemWorkbench.cs:368-376`).

**This is the opposite defect and must not be fixed with code.** The fix is authored content through
`recipegen`, and it belongs to module 16.

### ⛔ The correction: `forge-gem` is in **both** columns, and the ideal put it in the wrong one

`tier-system-ideal.md:560-562` says *"`imbue` and `forge-gem` are priced and routeable with zero
recipes … both always refuse `material.recipe-unknown`."* **Half wrong.** `forge-gem` is priced
(`materials.v1.json` `operations.forge-gem`, owner *"sockets (16)"*) and referenced by
`CostClassMatrix` (`:53`, `:104`, `:128`, `:133`) and by `MaterialRecipeCatalog.StrictLossLeak:284` —
but there is **no `ForgeGem` method on `ItemWorkbench` and no POST**. Measured: the only C# references
to `CraftOperation.ForgeGem` outside tests are those four, none of them an executor. **It cannot
refuse `material.recipe-unknown`, because nothing calls it at all.** `forge-gem` is an executor gap
**and** a content gap, and closing either alone changes nothing observable.

### The three-column summary this spec exists to keep straight

| Verb | Executor | Authored recipes | Which gap | Owner |
|---|---|---|---|---|
| `forge` | ✗ | 7 | **wiring** | this module (14) |
| `reroll-one` | ✗ | 5 | **wiring** | this module (15 by price, built here) |
| `reroll-all` | ✗ | 2 | **wiring** | this module (15 by price, built here) |
| `elevate` | ✗ | 10 | wiring | ⛔ **`spec-rarity-promotion.md`** — excluded here |
| `imbue` | ✓ | **0** | **content** | module 16, via `recipegen` |
| `forge-gem` | ✗ | **0** | **both** | module 16 (price) + content |

⚠ **The header's *"module 14 + module 16"* is the map's framing; the tuning file's own `owner` fields
say `reroll-one`/`reroll-all` belong to module 15 (`enhance-reroll`).** Recorded rather than
smoothed over — the verbs are built here because they share one workbench and one spend path, and the
ownership row is where the balance ask is filed.

---

## Relationship to `rarity-promotion`

[`spec-rarity-promotion.md`](spec-rarity-promotion.md) claims **`Elevate` exclusively** — its ten
recipes, its `MutationOpKind` member, its `promoted_from_ordinal` mark. **This spec covers only the
remaining unowned verbs and touches none of that.**

**Recommendation: run beside it, not after it — and expect this module to land first.**

- `rarity-promotion` is blocked on a **closed-enum amendment**: `MutationOpKind` has no promotion
  member, the enum is ask-first (`MutationOp.cs:11`), and `Repair` is filed ahead of it
  (`deployment-hierarchy-map.md:89`). That queue is real and outside either module's control.
- **This module needs no enum member at all.** Reroll's two op kinds already exist (`:19`, `:22`) and
  are already reserved in §5.3; forge writes **no** mutation op — it mints a new instance, exactly as
  `Upcycle` grants a new material without one.
- Sequencing an unblocked module behind a blocked one inherits a blocker it does not have.

⚠ **So the sibling's framing that `rarity-promotion` *"builds the first new executor and the workbench
pattern"* is likely to be wrong in practice.** Whichever lands first owns the shared verb scaffold;
on today's evidence that is this module, and `rarity-promotion` should copy it. **Named here so the
two specs do not each wait for the other.** Neither module may fork the pattern: one shape, six verbs
today, nine after both land.

---

## Design

### 1. Reroll — two verbs over a policy that is already written

```
Reroll (Server)
  └─ Replay(...)                                        ← identical to the five
  └─ TryResolve(playerId, instanceId, ...)              ← identical
  └─ TryRecipe(recipeId, CraftOperation.RerollOne|RerollAll, ...)
  └─ RerollPolicy.ValidateRerollable / TargetsFor / ValidateTargets
  └─ SeededRng.DeriveStream(DeriveOpSeed(instance, correlation),
        MutationOpKinds.StreamName(MutationOpKind.RerollValue|RerollAffix))
  └─ RerollPolicy.ValidatePostOp                        ← the post-condition, not an afterthought
  └─ _store.TrySpendAndApply(..., mutation)             ← identical
```

⛔ **Nothing new is invented.** The only judgement call is the mapping, and it is already made by the
enum's own doc comments: `reroll-one` redraws **the value of one affix inside its own range** →
`MutationOpKind.RerollValue` (`MutationOp.cs:18-19`); `reroll-all` redraws **identity, tier and value
of a chosen subset** → `RerollAffix` (`:21-22`). `RerollAffix`'s own doc says it is *"suppress +
append"*, which is why `MutationResult.Suppressed` and `Appended` both exist.

⚠ **`RerollAffix` needs `MutationResult.Appended` to actually be applied by the store.** Measured:
`RpgStore.AppendMutationOpUnlocked` applies `result.Suppressed` (`RpgStore.InstanceOps.cs:206-207`)
and **inserts nothing for `result.Appended`**. That is the same store-side seam
[`spec-enhance-track-wiring.md`](spec-enhance-track-wiring.md) closes. **Sequencing note, not a
dependency:** if that module lands first, `reroll-all` inherits a working append; if this one lands
first, it must close the same seam, and the two must not both write it. **Recommendation: let
`enhance-track-wiring` own that store change**, since it is that module's central defect, and have
`reroll-all` follow it.

### 2. Forge — a spend that mints, reusing the shipped mint path

`forge` is the one verb here that produces an **item**, not a mutation. Its shape is `Upcycle`'s
(spend → grant, no instance, no mutation op) with the grant replaced by a mint:

- `outputRef` names a base type → `EquipmentContainerBuild.From` assembles the container
  (`EquipmentContainerBuild.cs:47`).
- `Instantiator.TryInstantiate` freezes the rolls with **`InstanceOrigin.Craft`**
  (`Instantiator.cs:10`), seeded from `(recipe, correlation)` so a retry mints the identical item.
- The instance is saved on the **same transaction as the debit** — a spend with no item is theft, an
  item with no spend is duplication (`MutationOp.cs:124-125`'s own wording for the mutation case).

⛔ **The shipped comment that says forge cannot run is stale.** `ItemWorkbench.cs:189-193` states
forge *"**cannot** run: its recipes name `item.*` containers and no module has authored an
`effect_container` for a base type (module 14's own P4.1 note, unchanged)."* **Verified against code:
it no longer holds** — `EquipmentContainerBuild` builds the container from the base type rather than
requiring one to be authored, and `LootMintAt`'s Equipment arm already instantiates exactly that. The
comment must be corrected in the same change (DESIGN-GATE §3 rule 6: a correction that does not
propagate has not landed).

⚠ **`StrictLossLeak` (`MaterialRecipeCatalog.cs:282-306`) already guards forge-then-salvage from being
a net gain** — an invariant written for a verb nothing could run. It becomes live the moment forge
does; the build must confirm all 7 rows still pass it rather than assume they do.

### 3. `forge-gem` and `imbue` — named, separated, not built here

- **`imbue`: content only.** Its executor and POST are shipped and correct. Authoring `imbue` recipes
  goes through `recipegen`
  (`tools/seedsmith/seedsmith/adapters/items/recipegen`) — the same path the corpus's own
  `_meta.amendments` records for `recipe.031`/`recipe.032`. ⛔ **Never hand-add a row to
  `recipes.json`**: it carries `_meta.model`, `promptVersion` and `batch`, so it is generator output.
- **`forge-gem`: both gaps.** Recommendation in Open question 3; **not built in this module** without
  an owner decision, because an executor with zero recipes reproduces `imbue`'s exact situation one
  verb over, and building it changes nothing a player can observe.

### 4. ⛔ What this module explicitly does not touch

- `Elevate` — `rarity-promotion`'s, including its `MutationOpKind` member and `promoted_from_ordinal`.
- The `CraftOperation` enum — all ten members already exist; **zero are added**.
- `MutationOpKind` — reroll's two already exist; **zero are added**.
- Pricing. Every operation is already priced in `materials.v1.json`; **confirm, do not re-author.**
- The recipe corpus. Content is `recipegen`'s.

---

## Tech stack

C# .NET 8 (`FusionRpg.Server`, reading `FusionRpg.Core/Items/Mutation` and `Core/Items`), xUnit. No new
dependency. FE is out of scope for this initiative (a bench surface is `item-surfaces` module 20).
**SQL only inside `FusionRpg.Data`.**

## Commands

```powershell
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Reroll"
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Material"
dotnet test tests/FusionRpg.Data.Tests --filter "FullyQualifiedName~InstanceOp"
dotnet test tests/FusionRpg.Server.Tests
dotnet test tests/FusionRpg.E2E.Tests --filter "FullyQualifiedName~Workbench"
dotnet run --project tools/ItemSeedValidator
.\scripts\guard-dal.ps1
.\scripts\guard-actor-hub.ps1
python scripts/audit-overflow.py
python scripts/audit-magic-numbers.py --targets M1
```

## Project structure

| Path | Role |
|---|---|
| `src/FusionRpg.Server/ItemWorkbench.cs` | Verbs seven, eight and nine — `RerollOne`, `RerollAll`, `Forge`, same shape as the six. **`:189-193`'s stale "forge cannot run" comment corrected here** |
| `src/FusionRpg.Server/WorkbenchEndpoints.cs` | Three POSTs beside the existing six |
| `src/FusionRpg.Core/Items/Mutation/RerollPolicy.cs` | **Read only** — already built and tested; this module supplies the missing caller |
| `src/FusionRpg.Core/Items/EquipmentContainerBuild.cs` | **Read only** — forge's container assembly |
| `src/FusionRpg.Core/Items/Drops/LootMintAt.cs` | **Read only** — the Equipment mint arm forge reuses |
| `src/FusionRpg.Data/Sqlite/RpgStore.InstanceOps.cs` | The `result.Appended` insert `reroll-all` needs — **owned by `enhance-track-wiring`**, not duplicated here |
| `data/tuning/materials.v1.json` | `operations.forge` / `reroll-one` / `reroll-all` — **confirm, do not re-author** |
| `data/seed/items/recipes/recipes.json` | **Read only, generated.** `imbue`/`forge-gem` content is `recipegen`'s |

## Code style

```csharp
// Seventh verb, identical shape to the six. The op-kind mapping is NOT a judgement call: the enum's
// own doc says reroll-one "redraws the VALUE of one affix inside its own range" (RerollValue) and
// reroll-all "redraws identity, tier and value of a chosen subset" (RerollAffix). Commented because
// the recipe id and the op kind read as near-synonyms and a later session will otherwise swap them.
if (!TryRecipe(recipeId, CraftOperation.RerollOne, out _, out var recipeRefusal))
    return Refused("reroll-one", instanceId, recipeId, recipeRefusal);

// The op's own named stream, domain-separated per op kind, so adding a roll to one operation never
// shifts another's sequence — MutationOpKinds.StreamName's stated reason, not a convention.
var rng = SeededRng.DeriveStream(
    unchecked((ulong)RpgStore.DeriveOpSeed(instanceId, correlationId)),
    MutationOpKinds.StreamName(MutationOpKind.RerollValue));
```

⚠ For `Forge` the stream is keyed on `(recipeId, correlationId)` rather than an instance id, because
**there is no instance yet** — the same asymmetry `Upcycle` already has (`Replay(..., instanceId:
null)`, `ItemWorkbench.cs:196`). It must be commented, or a reader will "fix" it to match the others.

---

## Tunables

⭐ **This module introduces NO new balance number.** Every verb it builds is already priced, and the
prices are already in config:

| Number | Meaning | Owner |
|---|---|---|
| `operations.forge` — `souls` ×40 on `grade`, `substrate` ×4 on `grade`, `catalyst.forge` ×1 flat | Forge's price. **Already shipped** — confirm, do not re-author | `data/tuning/materials.v1.json` |
| `operations.reroll-one` / `operations.reroll-all` | Reroll's price, `rung`-variable. **Already shipped** | Same file |
| `RerollPolicy.RungLegMilli` / `CostMultMilli` inputs | Reroll's cost multiplier, already reading `EnhancementTuning` | `data/tuning/enhancement.v1.json` |

**Structural (stays `const`, with a comment saying why):** the closed ten-member `CraftOperation`
enum (`CostClassMatrix.cs:11-44` — *"adding a verb here is code"*), the closed ten-member
`MutationOpKind` (`MutationOp.cs:13-48` — *"Adding a member is ask-first"*), the closed 27-id material
vocabulary and the five `MaterialClass` spend classes, and `MutationLimits.MutationSeqCap`
(`MutationOp.cs:77`, which **throws rather than clamping**). Widening any of them is a reviewed change
and **not this module's**.

⚠ **A revision is the `version` field inside the existing file, not a new filename** — verified:
`data/tuning/materials.v1.json` carries `schemaVersion: 1` **and** `version: 1`, and
`data/tuning/enhancement.v1.json` carries both as well. (Counter-example verified in the same pass:
`data/tuning/base-types-gen.v1.json` carries **no** `version` field, so the rule is per-file and must
be checked, not assumed.)

⛔ **No hard progression ceiling.** Nothing here caps a magnitude. If the build finds one on the
forge or reroll path, it is removed or made a **configurable soft cap**; an absolute bound is derived
and **throws, never clamps** — a clamp turns *"your crafting stopped mattering"* into a bug with no
symptom.

## Numeric types

- Costs and forged magnitudes are **`long`**. `forge` prices on `grade` and both rerolls on `rung`, so
  every one of them scales with a ladder.
- **Widen before multiplying** (`(long)a * b`, never `(long)(a * b)` — the cast binds to the result,
  so the multiply has already overflowed); **divide by 1000 last, exactly once**; **overflow throws,
  never wraps** (`checked`, no silent `unchecked`).
- **Never `float`** for a magnitude: integer-exactness fails at `Θ` = 232, inside normal play, and
  `float` is non-deterministic across runtimes — disqualifying on a hashed, persisted path.
- `RerollPolicy.AnchorMultiplier` (`:53`) and `CostMultMilli` (`:235`) already return `long`;
  **this module must not narrow them at the call site.** A narrowing `(int)` cast is a cap
  (`ssot-power-scale.md` §11, PS-8) even when it is not named like one.
- `Seq`, `socketIndex`, `outputQty` and rung/grade indices are small identity ordinals — never
  magnitudes, never multipliers.

## ActorHub gate

**Contributes nothing; consumes nothing.** A craft cost is an economy number, and a forged or rerolled
item's affixes compose through the **existing** equipment path into `ActorHub` — the same channel that
already carries every dropped item's affixes. `LootMintAt`/`Instantiator` is the shipped mint path and
this module reuses it rather than forking one.

⛔ **No `*Composer*`, no private ChannelMods combat writer, no mode-local fold of the same numbers.**
`guard-actor-hub.ps1` must stay green. ⛔ **A forged item must be indistinguishable from a dropped one
at the Hub** — it differs only by `InstanceOrigin.Craft`, which is provenance, not a magnitude.

## Testing strategy

**Store tests run in memory**; disk only when the disk is the thing under test; a failed temp-delete
is a **failure**, never `catch { }`.

| Level | What it asserts |
|---|---|
| Unit | `reroll-one` maps to `MutationOpKind.RerollValue` and `reroll-all` to `RerollAffix` |
| Unit | Each verb registers its own named seeded stream, so adding a roll to one never shifts another |
| Unit | `RerollPolicy.ValidatePostOp` is enforced — a reroll that would violate the budget **refuses** |
| Unit | A recipe whose `operation` does not match the verb refuses by name, never silently proceeds |
| Unit | Forge mints with `InstanceOrigin.Craft`, seeded so a retry mints the identical item |
| Unit | `checked` throws rather than wrapping at the cost boundary |
| Integration | ⭐ **Every authored `forge` recipe executes end to end** — computed from the corpus, not from a count |
| Integration | ⭐ **Every authored `reroll-one` and `reroll-all` recipe executes end to end** — likewise |
| Integration | ⭐ **The debit and the product commit together** — a forge that fails to mint spends nothing, and a mint that happens is paid for |
| Integration | Replaying any of the three verbs with the same `(instanceId\|recipeId, correlationId)` is idempotent and returns the recorded result |
| Integration | `reroll-all`'s suppress **and** append both reach `effect_instance_atom` (guards the `RpgStore.InstanceOps.cs:206` seam) |
| Contract | ⭐ **Every recipe whose `operation` has an executor is reachable by a POST; every operation with no executor is named in an explicit, reviewed exclusion list** — the assertion that makes a future stranded verb fail loudly instead of silently |
| Contract | `CraftOperation` and `MutationOpKind` membership pinned — **closed vocabularies, pinning is correct**, and this module changes both by **zero** |
| Contract | All 7 `forge` rows still pass `MaterialRecipeCatalog.StrictLossLeak` once the verb is live |
| Contract | `ItemSeedValidator` green |
| Guard | `guard-dal.ps1`, `guard-actor-hub.ps1` green |

⛔ **No test asserts a population count or generated text.** Not `67` recipes, not `24` stranded, not
`7` forge rows, not `35` tempers, and not any authored `name` or flavour string. Every one of those
moves the day content ships, and the "fix" would be to bump the number. **Assert instead: every
recipe whose `operation` is `forge` executes; every `reroll-*` row executes; the operation → executor
join is total over the non-excluded set; ids are unique; every `outputRef` resolves.** Print the
scale; never assert it.

## Boundaries

**Always**
- Copy the shipped six-verb shape exactly — `Replay` → `TryResolve` → `TryRecipe` → named stream →
  policy → `TrySpendAndApply`.
- Register each op's named stream, even where the operation rolls nothing.
- Commit the debit and the product in one transaction.
- Keep the executor gap and the content gap in separate columns, and say which any new finding is.
- Correct `ItemWorkbench.cs:189-193`'s stale forge comment in the same change that makes it stale.
- Confirm the shipped prices rather than re-authoring them.
- Run store tests in memory.
- Commit via MCP `repo-git.commit` with explicit `paths`.

**Ask first**
- ⛔ **Any `MutationOpKind` member.** The enum is closed and *"adding a member is ask-first"*
  (`MutationOp.cs:11`). **This module needs none** — but if one is ever proposed, it **queues behind
  `Repair`** (filed first, `deployment-hierarchy-map.md:89`), **then `Elevate`**
  (`spec-rarity-promotion.md`), **then `item-upgrade-tree`'s.**
- ⛔ **Any `CraftOperation` member.** Closed; *"adding a verb here is code."* This module needs none.
- ⛔ **Any `ssot-enhancement.md` §5.3 `op_kind` amendment.** Noted while reading: §5.3's table lists
  nine values and **does not list `socket-imbue`**, which the enum mints at `MutationOp.cs:42-47`.
  Pre-existing drift, not caused here — named so the next amendment reconciles it.
- Whether to build a `forge-gem` executor at all (Open question 3).
- Authoring `imbue` or `forge-gem` recipes — content, through `recipegen`, module 16's call.
- Who owns the `RpgStore` append insert if the two specs land out of order.

**Never**
- ⛔ Build `Elevate` here. It is `spec-rarity-promotion.md`'s, including its enum member and its
  `promoted_from_ordinal` mark.
- ⛔ Hand-edit `data/seed/items/recipes/recipes.json`. It carries `_meta.model`, `promptVersion`,
  `batch` and an amendment log — generator output. **Fix the generator and regenerate.**
- ⛔ Fork the workbench pattern. One shape, however many verbs.
- ⛔ Write a private cost curve. Every operation is priced in `materials.v1.json`; a second one is
  the drift bug with a delay fuse.
- ⛔ Let a forged item reach the Hub by any path a dropped item does not already use.
- Narrow a `long` cost to `int` at a call site. That is a cap, and it is not exempt.
- Add a destroy outcome anywhere on this path — `EnhanceOutcome` deliberately has none
  (`MutationOp.cs:50-53`).
- Assert a population count or a generated string.

## Success criteria

1. ⭐ **All 7 authored `forge` recipes execute end to end** and mint a real, saved instance with
   `InstanceOrigin.Craft` — computed from the corpus, not from a pinned count.
2. ⭐ **All authored `reroll-one` and `reroll-all` recipes execute end to end**, through the
   already-built `RerollPolicy`.
3. `ItemWorkbench` exposes nine verb methods in one shape, with a POST for each.
4. **Zero members added to `CraftOperation` and zero to `MutationOpKind`** — asserted by a pinned
   closed-vocabulary test.
5. `Elevate` remains untouched and unclaimed by this module.
6. The debit and the product commit together; replay is idempotent for all three new verbs.
7. `reroll-all`'s suppress and append both reach `effect_instance_atom`.
8. `ItemWorkbench.cs:189-193`'s *"forge cannot run"* comment is corrected, with the evidence that
   retired it.
9. `imbue` and `forge-gem` are documented as a **content** gap with a named owner and a named path
   (`recipegen`, module 16) — and neither is closed by hand-editing seed data.
10. A contract test makes any future priced-but-unroutable verb fail loudly rather than silently.
11. Core, Data, Server and E2E suites green; `ItemSeedValidator` green; `guard-dal.ps1` and
    `guard-actor-hub.ps1` green; `audit-overflow.py` reports no new critical.

## Open questions

1. ⭐ **Does this module or `rarity-promotion` land the shared verb scaffold?**
   **Recommendation: this one**, because it is blocked by nothing while `rarity-promotion` waits on a
   two-deep closed-enum queue. Answerable now; recorded because the sibling spec currently assumes the
   opposite and a later session would otherwise stall each on the other.
2. **Who owns the `RpgStore.InstanceOps.cs:206` append insert?** **Recommendation:
   `enhance-track-wiring`**, where it is the central defect. `reroll-all` then depends on it landing
   first — a **build-order** dependency, not a design one, and cheap either way.
3. **Build a `forge-gem` executor now, or wait for content?** **Recommendation: wait, and file the
   content ask.** An executor with zero recipes reproduces `imbue`'s situation exactly — a verb that
   refuses `material.recipe-unknown` forever changes nothing a player can see. Build it **in the same
   change as its first recipes**, so the two halves land together and the gap never re-splits.
4. **Does `forge` mint at a fixed item level, or one derived from the recipe's grade?**
   **Recommendation: derived from the substrate grade the recipe already spends**, because the grade
   leg is what distinguishes `recipe.001` (`substrate.humanoid.crude`) from `recipe.023`
   (`substrate.humanoid.fine`) — the corpus already encodes the intent, and a flat level would make
   the three tiers of forge recipe produce identical items.
5. **Does `craft-risk-ladder`'s crafting potential apply to a reroll?** It is that module's dial, not
   this one's. **Recommendation: yes, and keyed per `CraftOperation` id** so the answer is a tuning
   row rather than a code change — the same recommendation `spec-rarity-promotion.md` Open question 3
   makes for `elevate`, and the two should be decided in one review rather than stacking unexamined.

---

## Gate status

**DESIGN-GATE §5, honestly.** Read this session: `DESIGN-GATE.md` §2/§3/§5, `tunables-ssot.md`,
`validation-ssot.md`, `ssot-power-scale.md` §10/§11/PS-8, `ssot-enhancement.md` §5.3, `CLAUDE.md`
"Numeric overflow", `AGENTS.md` Hard boundaries, both sibling specs in this directory, and the
initiative map. Every count above was **re-derived with `python` over the shipped corpus**, not quoted
(§3 rule 5): the ideal's *"24 of 67"* reproduces exactly, its verb list does not, and its `forge-gem`
claim is wrong in a way that changes what the fix is. Three claims carried only by comments were
checked against code; **one of them (*"forge cannot run"*) is stale and is corrected here.**
**The §5 boundary box remains untickable** — this session wrote no `tasks/sessions/*.json` record,
because `/session-start` does not exist in this harness. Unchanged from the map's own gate status, and
stated rather than hidden.
