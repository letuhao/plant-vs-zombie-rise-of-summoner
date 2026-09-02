# Spec: kind-value-guard (E29)

**Status: DRAFTED 2026-09-03**, from [effect-atom-ideal.md](../effect-atom-ideal.md) §W7.2 and the
capability map's [§12](../effect-atom-map.md). Module **E29**, Wave 7. **No dependencies.**

**What it owns: turning every silent no-op into a load-time refusal.** Ten of the twelve atom kinds
accept a *value* from an enumerable vocabulary — a status id, a currency, a grid item type, a board verb
— and **none of those vocabularies is enforced.** A wrong value validates, compiles, reaches the
executor, matches no case, and does nothing forever.

---

## 1. The defect, and why it is the cheapest module in the wave

Shape validation is already good: `ParamSchema.Validate` refuses an undeclared key
(`ParamSchema.cs:67-68`) and refuses a declared key carrying a `NotImplementedNote` (`:70-71`). Trigger
eligibility is enforced (`AtomKindRegistry.cs:93-103`).

**Value validation exists for exactly one kind.** `AtomKindRegistry.Validate`'s rule **G6** checks that
`stat.modify`'s `channel` is one of the 11 primary channels — and its own comment says why:

> *"an unknown PRIMARY channel used to pass validation and then write nothing, because
> `ModifierBag.Upsert` only checks for a non-empty name. The registry declared `PrimaryChannels` and
> never read it, which made the list documentation rather than a rule."*

**That reasoning applies verbatim to ten other kinds, and was never extended to them.** Both G6 guards
are scoped `if (string.Equals(kindId, "stat.modify", …))` — `AtomKindRegistry.cs:64` and `:79` — and
`AtomRowValidator.ValidateOp` returns `Ok` for every kind but the two stat ones
(switch at `AtomRowValidator.cs:279-284`, return `:285`; `:265-271` is that method's doc comment).

### 1.1 ⛔ The worst case is `stat.derived`, and the code hands it off to a check that never runs

`AtomRowValidator.cs:313-314` (this spec originally cited `:296`, a closing paren):

```csharp
var kind = composeKindOf(channel);
if (kind is null) return AtomRejection.Ok; // unregistered channel is G6's job, not this check's
```

**G6 never runs for `stat.derived`.** So `crit.rat` where `crit.rate` was meant — one letter off, out of **267** valid
ids — validates, binds, compiles, and writes nothing forever. That is verbatim the failure the kind's own
D6 quarantine note says the module exists to prevent:

> *"A bind would have been accepted and then done nothing forever, which is the exact failure this module
> exists to prevent."*

**It is worth 24× more here than on `stat.modify`** — 267 ids to mistype instead of 11.

---

## 2. The vocabularies, and where each one's SSOT lives

Every row is a real, enumerable set. **The guard reads the SSOT; it never copies a list** — that is the
mistake G6's own comment describes.

| Kind | Param | Size | SSOT |
|---|---|---:|---|
| `stat.modify` | `channel` | **11** | `StatChannels.All` (`ModifierOp.cs:26`) — **already enforced** |
| `stat.derived` | `channel` | **267** | `DerivedStatRegistry.CreateDefault().AllRegistered` |
| `status.apply` | `status` | **21** | `StatusCatalogBootstrap.RegisterAll` |
| `status.clear` | `status` | **21** vocabulary, **4** executable on the lawn | same catalog; the lawn switch is `InjectorEffectActionSink.cs:307-318` |
| `resource.economy` | `currency` | **5** | `CheatActions.SetEconomy`'s switch (`CheatActions.cs:599-620`) — `sun`, `money`, `points`, `maxSun`, `maxMoney` |
| `resource.economy` | `op` | **2** | `InjectorEffectActionSink.cs:453` — `add`/`+`, else set |
| `resource.delta` | `channel` | **6** after E28 (**1** today) | `ResourceIds` |
| `shield.grant` | `element` | **6** + none | `ElementRoster.Concrete` — already strict-parsed and refused at `EffectBag.cs:585-594` |
| `shield.grant` | `sourceClass` | **3** | `EffectBag.cs:597-599` — `aura`, `innate`, else skill |
| `board.action` | `op` | **4** | `DebugActions.BoardAction`'s switch |
| `grid.spawn` / `grid.clear` | `gridItemType` | **12** | the `GridItemType` enum |
| `box.set` | `boxType` | **8** | the `BoxType` enum |
| `spawn.entity` | `kind` | **3** | `plant` · `zombie` · `bullet` |

### 2.1 Two registry comments this module must correct

- **`AtomKindRegistry.cs:210-211`** claims the injector exposes `maxSun`/`maxMoney` *"which FA9 does
  not"*. **False** — `ExecEconomy` passes `currency` through unfiltered and `SetEconomy` handles both.
  The vocabulary is **5**, not 3.
- **`resource.economy`'s `op` param** is unvalidated and effectively two-valued, so **a typo silently flips `add`
  into `set`** — the most damaging silent no-op in the set, because it succeeds loudly at the wrong thing.

---

## 3. The contract

**One extension point, not eleven special cases.** `AtomKind` gains an optional value-vocabulary
declaration per param — a delegate or a named vocabulary id resolved from the SSOT at validation time —
and `AtomKindRegistry.Validate` checks it generically.

Rules:

1. **An unknown value is a refusal**, `BadParamValue`, naming the kind, the param, the offending value
   and the vocabulary size. G6's existing message is the template.
2. **The guard resolves the SSOT; it never holds a copy.** A vocabulary that drifts from its source is
   the defect, not the fix.
3. **`none`/absent stays legal where the schema already allows it.** This module refuses *wrong* values,
   not *missing* ones — `ParamSchema` already owns required-ness.
4. **Where a vocabulary differs per runtime, the guard uses the union and the executor still reports.**
   > **⛔ CORRECTED 2026-09-03 — the original example was false.** It claimed *"`status.apply` reaches
   > 21 in battle and 8 on the lawn; a `wither` atom is inert on one runtime."* **`wither` works on the
   > lawn.** It is `StatusKind.OverTime` / `PayloadKind.PulseHp` — one of the **13 overlay-authored**
   > statuses resolved inside `StatusRuntime`, which is mounted in the injector
   > (`EffectRuntime.cs:19,31`). The **8** is the size of the Unity CC switch
   > (`DebugActions.cs:861-909`) — what FA2 is emitted for, not what `status.apply` reaches.
   > `spec-plant-side-status.md` §2c had this right and this spec contradicted it.
   >
   > **The rule itself stands unchanged**: the guard uses the union, and a runtime that cannot execute
   > a legal value refuses **at execute time with a named reason**, never at load.
5. **`stat.derived`'s channel check lands here**, closing the hand-off `AtomRowValidator.cs:296` makes to
   a check that never ran.

---

## 4. What this module must NOT do

- **Refuse content that is legal on one runtime and inert on another** — rule 4. That is E28's problem
  and a reporting concern.
- **Copy a vocabulary into the guard.** Read the SSOT.
- **Change any executor.** This module refuses earlier; it does not make anything newly work.
- **Clamp, coerce, or best-match a wrong value.** A refusal names the value. Silent correction is the
  same class of defect as a silent no-op.
- **Add a param, a kind, or a trigger.**

---

## 5. Testing strategy

**One planted violation per vocabulary in §2** — thirteen tests, each asserting the refusal names the
offending value. Plus:

| # | Test | Proves |
|---|---|---|
| 1 | `stat.derived` with `crit.rat` where `crit.rate` was meant is **refused at load** | The 267-id hand-off gap is closed |
| 2 | `resource.economy` with `currency: "souls"` is refused | Wave 7's own worked example of the silent no-op |
| 3 | `resource.economy` with `op: "addd"` is refused, **not treated as `set`** | The most damaging case: succeeding at the wrong thing |
| 4 | `status.apply` with `wither` is **accepted** (legal in battle, inert on lawn) | Rule 4 — the guard does not over-refuse |
| 5 | `grid.spawn` with `gridItemType: 999` is refused | Numeric vocabularies too, not just strings |
| 6 | Every one of the **21 catalog statuses** is accepted | The guard reads the SSOT rather than a stale subset |
| 7 | Adding a status to the catalog makes it **immediately valid**, with no guard edit | Proves rule 2 mechanically |
| 8 | Every **shipped** atom in `data/seed/atoms/` still validates | The module is additive to existing content |

---

## 6. Acceptance criteria

1. All thirteen vocabularies in §2 are enforced, each reading its SSOT.
2. Every refusal names kind, param, value and vocabulary size.
3. `stat.derived`'s registered-channel check runs, and `AtomRowValidator.cs:313-314`'s stale comment is
   corrected to point at it.
4. `AtomKindRegistry.cs:210-211`'s `maxSun`/`maxMoney` claim is corrected; the currency vocabulary is 5.
5. A vocabulary gaining a member requires **no guard change**, proven by test 7.
6. All 21 shipped atoms and all 98 authored families still validate.
7. No executor behaviour changes.

---

## 7. Dependencies and cross-program hazards

| | |
|---|---|
| **Depends on** | Nothing. May run first in Wave 7 |
| **Unblocks** | **E30** — pool members are validated by this same machinery, extended per member |
| **Pairs with E28** | E28 widens `resource.delta` to 6 and `status.clear` to 21; **E29's vocabularies must be written to read the SSOT so they widen automatically**, not to hard-code today's narrower set |
| **empire-economy** | Its 18 stock ids (`loam`, `soul`, `essence.*`, `shard.*`) share **no member** with the atom currency vocabulary. After this module, `currency: "loam"` is a **hard load-time refusal** — correct, but it means no empire currency is atom-authorable, and that should be written down rather than discovered |
