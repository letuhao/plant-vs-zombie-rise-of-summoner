# Spec: Rarity promotion (`rarity-promotion`)

**Initiative:** `species-gear-chain` ([map](../species-gear-chain-map.md)) · **Module:** `rarity-promotion`
**Owning program:** `item` module 15 (`enhance-reroll`)
**Depends on:** `craft-risk-ladder`, `tier-propagation-contract`
**Status:** spec, 2026-09-13. Awaiting owner approval. No build authorized.
**Source ideal:** [gear-climb-ideal.md](../gear-climb-ideal.md) § The shape 1 (E6)

---

## Objective

**Give the ten-rung rarity ladder a climb.**

⭐ **This is not a design problem.** `ssot-rarity.md` §3.7 is a complete operation spec, its balance is
measured (§7.2), its cost is priced, ten recipes are authored, and the consumer is registered.
**Everything exists except the ability to persist the operation.**

**Measured this session:** `data/seed/items/recipes/recipes.json` holds **67 recipes**, of which
**10 are `elevate`** — complete, with `outputKind: "mutation"`, full `costLines`, and cost bands:

```json
{ "id": "recipe.009", "name": "Elevate: Forge Rarity", "operation": "elevate",
  "outputKind": "mutation", "outputQty": 1, "frame": "any",
  "costLines": [ {"material": "substrate.humanoid.crude", "costBand": "standard"},
                 {"material": "shard.chaff",              "costBand": "standard"},
                 {"material": "catalyst.temper",          "costBand": "cheap"} ],
  "soulsCostBand": "standard" }
```

**They are authored, priced, shipped — and unreachable.** Until this lands, the ladder has no climb.

---

## What exists today — verified against code and the shipped corpus

### Built

| Fact | Evidence |
|---|---|
| **`CraftOperation.Elevate` exists** — *"Promote an item's rarity rung. Owned by module 15."* | `CostClassMatrix.cs` (the 7th of ten members) |
| **`operations.elevate` is priced**, four legs, all `rung`-linear: `souls` ×60, `substrate` ×2, `shard` ×1, `catalyst.temper` ×1 | `data/tuning/materials.v1.json` |
| **Ten `elevate` recipes are authored** | `data/seed/items/recipes/recipes.json` — counted |
| **`RarityLadder.PromoteFrom(rarityId) => 1`** — *"D7's own registered rule: no rung is drop-only. All ten promote from a lower rung"* | `Items/RarityLadder.cs:22` |
| The `promoted_from_ordinal` mark is **already specified** as a required column and a §3.7 rule | `item/ssot-rarity.md:246` rule 6; `:473` N6; `spec-rarity-bands.md:121` |
| The exact executor pattern: `TryResolve` → `TryRecipe(recipeId, CraftOperation.X, …)` → named seeded stream → policy resolve → `AppendMutationOp` | `FusionRpg.Server/ItemWorkbench.cs:240-269` (`Enhance`) |
| The ledger, **idempotent per `(instance_id, correlation_id)`** | `RpgStore.InstanceOps.cs:46-66` |

### Real gap — exactly two things

1. ⛔ **`MutationOpKind` has no promotion member.** Ten members (`Enhance` … `SocketImbue`), closed,
   and its own doc comment says *"Adding a member is ask-first."* **Without it the operation cannot be
   persisted at all** — which is the entire reason ten priced recipes sit unreachable.
2. ⛔ **`ItemWorkbench` exposes six public verbs** — `Salvage` (`:164`), `Upcycle` (`:194`),
   `Enhance` (`:240`), `SocketAdd` (`:293`), `SocketInsert` (`:338`), `SocketImbue` (`:378`).
   **No promotion verb is among them.**
   ⚠ **Corrected 2026-09-13.** An earlier draft listed *"`Upcycle` (:198), `Temper` (:248), `Bore`
   (:300), `Socket` (:347), `Imbue` (:386)"* — **those are not method names.** They are
   `CraftOperation` arguments on `TryRecipe(...)` lines *inside* `Enhance`/`SocketAdd`/`SocketInsert`.
   The draft read the argument line and named it as the verb. Promotion is therefore the **seventh**
   verb, not the sixth.

### ⚠ The queue, stated honestly

`Repair`'s `op_kind` was **filed first** (`deployment-hierarchy-map.md:89`). So `Repair` takes the
eleventh slot and `Elevate` the twelfth. **This is a filing-order fact, not a blocker** — both are
additions to the same closed enum and both need the same reviewed amendment. Module 15 is carrying
**three** queued asks now: `Repair`, `Elevate`, and `craft-risk-ladder`'s supersession of §4.

---

## Design

### 1. The amendment, the member, the executor, the mark

- **One reviewed amendment** adding an elevate row to `ssot-enhancement.md` §5.3's reserved `op_kind`
  table, filed on the item program alongside `Repair`.
- **One `MutationOpKind` member**, so the operation can be persisted.
- **One executor + one POST** — the **seventh** verb — copying the shipped `ItemWorkbench` /
  `WorkbenchEndpoints` pattern exactly: same resolve, same recipe lookup, same named seeded stream,
  same `AppendMutationOp`. `Enhance` (`:240-269`) is the closest template.
- **The `promoted_from_ordinal` mark surfaced on the item card.** This is the answer to PoE's
  documented regal-confusion bug report, and **on a ten-rung ladder it matters more than it did on
  four**: a Cultivated promoted to Fused and a natural Fused are different items, and the player has
  no other way to tell.

### 2. ⛔ Promotion is additive only — the rule that makes it safe

`ssot-rarity.md` §3.7 rule 3 and PoE's own Alteration/Chaos-vs-Regal split both say the same thing:
**promotion adds, it never re-rolls.** Existing affixes carry across untouched.

This is what lets promotion sit safely on `craft-risk-ladder`: the craft itself ruins nothing, so the
only degradation is the chassis wearing — a different axis. **Rerolling is I7's separate verb.**

### 3. ⛔ One rung per operation — and the item ladder has no rung arithmetic to do it with

**This is a real gap, not a detail.** There are two ladders and they are not interchangeable:

| | `Items/RarityLadder.cs` (item) | `Creatures/CreatureRarityLadder.cs` (creature) |
|---|---|---|
| Members | `RungIds`, `PromoteFrom`, `IsPityGuarded` — **that is the whole file** | `RungCount`, `OneRungAbove`, `OneRungBelow`, `RungsBelow`, `IsTopRung`, `IsBottomRung`, `AtLeast`, `AtMost`, `All` |
| Keyed on | `string rarityId` | `enum CreatureRarity` |
| Ordinals | **10, 20, …** | **0..9** |

⛔ **`Items/RarityLadder` has no `IsTopRung`, no `OneRungAbove`, no `RungCount`.** An earlier draft's
code sample called `CreatureRarityLadder.IsTopRung(current)` on an **item** rarity — wrong type, wrong
ordinal scheme; **it would not compile.**

And `tier-propagation-contract` does not supply it: its T-3 fix is scoped to the *creature* ladder,
its Open question 1 recommends **allowlisting** `RarityLadder.RungIds` rather than extending it, and
its Boundaries forbid collapsing the two ladders. **So this module's declared dependency on
`tier-propagation-contract` is satisfied by nothing.**

**Resolution (Open question 4): item-side rung arithmetic — `IsTopRung` / `OneRungAbove` over the
string-keyed, 10/20-ordinal item ladder — is this module's own deliverable**, built beside the
executor and mirroring the creature ladder's shape without sharing its type.

### 4. Risk comes from the ladder, not from this verb

⛔ **Promotion has no failure chance of its own.** Risk is a property of the **item**, in
`craft-risk-ladder`'s one graduated ladder. Giving promotion a private chance is the rejected
alternative — D4's documented evidence is that players rejected a finite, non-refundable budget on an
irreplaceable item, and Blizzard never removed it, only papered over it.

---

## Tech stack

C# .NET 8 (`FusionRpg.Core/Items/Mutation`, `FusionRpg.Server`), xUnit. No new dependency.
**SQL only inside `FusionRpg.Data`.**

## Commands

```powershell
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Mutation"
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Rarity"
dotnet test tests/FusionRpg.Data.Tests --filter "FullyQualifiedName~InstanceOp"
dotnet test tests/FusionRpg.Server.Tests
dotnet run --project tools/ItemSeedValidator
.\scripts\guard-dal.ps1
.\scripts\guard-actor-hub.ps1
python scripts/audit-overflow.py
```

## Project structure

| Path | Role |
|---|---|
| `docs/architecture/item/ssot-enhancement.md` §5.3 | The reviewed `op_kind` amendment |
| `src/FusionRpg.Core/Items/Mutation/MutationOp.cs` | The new member |
| `src/FusionRpg.Core/Items/…/ElevatePolicy.cs` | Pure resolve; no I/O |
| `src/FusionRpg.Server/ItemWorkbench.cs` | The sixth verb, same shape as the five |
| `src/FusionRpg.Server/WorkbenchEndpoints.cs` | The POST |
| `data/tuning/materials.v1.json` | `operations.elevate` — **confirm, do not re-author** |

## Code style

Copy the shipped verb shape rather than inventing one:

```csharp
// Sixth verb, identical shape to Temper/Bore/Socket/Imbue. Named seeded stream keyed on
// (instance, correlation) so a replayed op re-derives the identical result and the ledger's
// UNIQUE(instance_id, correlation_id) makes it idempotent.
if (!TryRecipe(recipeId, CraftOperation.Elevate, out var recipe, out var recipeRefusal))
    return recipeRefusal;
if (CreatureRarityLadder.IsTopRung(current))      // check FIRST — OneRungAbove throws at the top
    return Refusal.AlreadyTopRung(current);
```

⚠ `MutationOpKinds.StreamName` is *"one per op kind, recorded even when the operation rolls nothing,
so adding a roll later never shifts another operation's sequence."* **Register the stream even though
promotion is additive and rolls nothing** — that is the point of the rule.

---

## Tunables

| Number | Meaning | Owner |
|---|---|---|
| `promoteCostSoulsMilli` per rung + the material legs | The climb's price. **Already priced** as `operations.elevate` — confirm, do not re-author | `data/tuning/materials.v1.json` |
| Whether a rung's promotion is enabled at all (`promote_from`) | Already a per-rung registry key with a named consumer; **1 on all ten rungs today** | `rarity_budget` via `RarityBudgetKeys` |

⚠ **A cost ladder owes a `ssot-power-scale.md` §10 row.** `promoteCostSoulsMilli` prices on `rung`,
so it is a cost ladder. §10 rows 6, 26, 27, 31 and 33 are the precedent, and **row 18 shows an
authored per-rung table still earns one.** This is a cross-program ask against `power`.

⛔ **No hard progression ceiling.** Promotion cost is a **configurable soft cap**, never a hard stop;
an absolute bound is derived and **throws, never clamps** — a clamp turns *"your gear stopped
mattering"* into a bug with no symptom.

**Structural (stays `const`, with a comment saying why):** the **one-rung-per-operation** rule and the
**additive-only** rule. They are §3.7 legality — the contract — and a tunable that could relax them
would be a different feature.

## Numeric types

- Costs are **`long`**, and they price on `rung`, so they scale with the ladder.
- **Widen before multiplying** (`(long)a * b`, never `(long)(a * b)`); **divide by 1000 last, exactly
  once**; **overflow throws, never wraps**.
- **Never `float`** — integer-exactness fails at `Θ` = 232, inside normal play, and `float` is
  non-deterministic across runtimes, which is disqualifying on a hashed/persisted path.
- `promoted_from_ordinal` is a small identity `int` — an ordinal, never a magnitude, and **never a
  multiplier**.

## ActorHub gate

**Contributes nothing; consumes nothing.** A promoted item's affixes compose through the existing
equipment path into `ActorHub`, exactly as before — promotion is additive, so it adds affixes through
the same channel that already carries them.

⛔ **No `*Composer*`, no private ChannelMods combat writer.** `guard-actor-hub.ps1` must stay green.

⛔ **Rarity must not become a magnitude multiplier.** `ssot-rarity.md` §3.6 bans `CurveInput.Rarity`
on item containers, and the reason is measured: *"a multiplier on the rung makes rarity dominant and
destroys the overlap."* A promoted item is **not** a scaled item.

## Testing strategy

**Store tests run in memory**; a failed temp-delete is a **failure**, never `catch { }`.

| Level | What it asserts |
|---|---|
| Unit | ⭐ **Promotion is additive** — every pre-existing affix survives with identical identity, tier and value |
| Unit | Exactly one rung per operation |
| Unit | Promoting at the top rung **refuses** via `IsTopRung`, and never reaches `OneRungAbove`'s throw |
| Unit | `promoted_from_ordinal` is written and reflects the **original** ordinal, not the previous step, across two promotions |
| Unit | ⭐ **Promotion has no private failure chance** — it succeeds whenever potential remains, deferring entirely to `craft-risk-ladder` |
| Unit | The named seeded stream is registered even though promotion rolls nothing |
| Unit | Cost resolves from `operations.elevate` and matches the shipped coefficients |
| Unit | `checked` throws rather than wrapping at the boundary |
| Integration | ⭐ **All ten authored `elevate` recipes execute** — the acceptance test for the whole module |
| Integration | Replaying the same `(instanceId, correlationId)` is idempotent and returns the recorded result |
| Contract | `MutationOpKind` membership is pinned (**closed vocabulary — pinning is correct**, and this module changes it by exactly one) |
| Guard | `guard-dal.ps1`, `guard-actor-hub.ps1` green |

⛔ **No test asserts the recipe count.** 67 is a reading that grows when content ships. Assert that
**every recipe whose `operation` is `elevate` executes**, computed from the corpus.

## Boundaries

**Always**
- Copy the shipped workbench verb shape exactly.
- Check `IsTopRung` before `OneRungAbove`.
- Register the op's named stream even though it rolls nothing.
- Surface `promoted_from_ordinal` on the card — it is a §3.7 rule, not a nicety.
- Run store tests in memory.
- Commit via MCP `repo-git.commit` with explicit `paths`.

**Ask first**
- ⛔ **The `MutationOpKind` member.** The enum is closed and *"adding a member is ask-first"* — and
  `Repair` is filed ahead of it.
- The `ssot-enhancement.md` §5.3 amendment.
- The `ssot-power-scale.md` §10 row for the cost ladder.
- Re-authoring `operations.elevate` — it is already priced; confirm it instead.

**Never**
- ⛔ Let promotion re-roll or re-tier an existing affix. §3.7 rule 3, and PoE's Blessing-orb complaint
  is the documented evidence.
- ⛔ Give promotion its own failure chance. Risk lives in one ladder, on the item.
- ⛔ Make rarity a magnitude multiplier. `CurveInput.Rarity` is already banned on item containers.
- Add a destroy outcome anywhere on this path — `EnhanceOutcome` deliberately has none.
- Add a hard ceiling to the promotion cost curve.
- Assert a population count.
- Claim promotion changes what an item *is*. It is in-place (`outputKind: mutation`, no `outputRef`);
  changing the chassis is **`item-upgrade-tree`** (E5), a different verb.

## Success criteria

1. ⭐ **All ten authored `elevate` recipes execute end to end** — the thing that has never been true.
2. `MutationOpKind` gains exactly one member, under a reviewed amendment.
3. `ItemWorkbench` exposes a sixth verb in the same shape as the six, with a POST endpoint.
4. Promotion is provably additive: every affix survives identically.
5. `promoted_from_ordinal` is written, correct across multiple promotions, and visible on the card.
6. Promotion has no private failure chance; risk defers to `craft-risk-ladder`.
7. Top-rung promotion refuses cleanly; the ladder's throw is never reached.
8. Costs resolve from the shipped `operations.elevate` without re-authoring; the §10 row is filed.
9. `guard-dal.ps1`, `guard-actor-hub.ps1` green; Core, Data and Server suites green;
   `ItemSeedValidator` green.

## Open questions

1. *(Not a question — moved out.)* `Repair` takes the eleventh `op_kind` slot because
   `deployment-hierarchy` filed first. Settled by filing order; recorded in § Design so a later
   session does not re-derive it.

4. ⭐ **Who owns item-side rung arithmetic?** `Items/RarityLadder` has none and
   `tier-propagation-contract` will not add it (§ Design 3). **Recommendation: this module builds it**
   — ~20 lines mirroring the creature ladder, and promotion is its only caller today. The alternative,
   widening `tier-propagation-contract`'s scope, couples a correctness fix to a refactor.
2. **Does the species cost multiplier apply to `elevate`?** `species-cost-shaping` defers this to
   here. **Recommendation: yes** — promotion is exactly the top-of-tree step MH gates on a species
   part. But it should be balanced **with** the promotion cost curve in one review, not stacked
   unexamined.
3. **Does promotion consume more crafting potential than a temper?** **Recommendation: yes, keyed per
   `CraftOperation` id** so the dial exists without a code change — a rung climb is a bigger event
   than a value reroll, and the ladder already supports differentiating.
