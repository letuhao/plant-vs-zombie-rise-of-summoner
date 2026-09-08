# Spec: lawn-element-bind (E27)

**Status: DRAFTED 2026-09-03**, from [effect-atom-ideal.md](../effect-atom-ideal.md) §W7.2 defect 2 and
the capability map's [§12](../effect-atom-map.md). Module **E27**, Wave 7. **No dependencies.**

**What it owns: making elements real on the lawn.** Battle assigns an actor's element from its setup.
The lawn never does — so every plant and zombie resolves as `ActorElementTypes.Neutral`, and **196 of
the 267 registered derived channels are element-expanded and therefore inert there.**

---

## 1. The defect

**A grep of all of `src/` finds no call site passing `elementTypes:`.** The parameter exists and is
optional, so every lawn actor takes the default:

| | Evidence |
|---|---|
| The factory defaults to Neutral | `StatContextFactory.cs:33` and `:61` — `ElementTypes = elementTypes ?? ActorElementTypes.Neutral` |
| The lawn calls it without the argument | `InjectorCombatBridge.cs:69-83` (`ForZombie`/`ForPlant`), and `InjectorStatusBridge.cs:51-64` does the same |
| The hub propagates whatever the context carried | `ActorHub.cs:47` — `ElementTypes = ctx.ElementTypes` |
| **Battle does it properly** | `BattleEngine.cs:36` — `ActorElementTypes.Create(setup.ElementPrimary, setup.ElementSecondary == setup.ElementPrimary ? null : setup.ElementSecondary)` |

**Sorted: wiring gap.** Every piece exists — the type, the factory parameter, the roster, the matrices,
the 196 channels and their readers. **One argument is never supplied.**

**The blast radius is the largest in Wave 7.** 28 channel families expand over `omni` + 6 elements
(`DerivedStatChannels.CombatChannelFamilies`), so **196 channels** are addressable, read by
`OverlayCombatCalculator` and `CombatDerivedReader`, and always resolve against a Neutral actor on the
lawn. Shipped species content already assigns `elementPrimary`, so the data exists and does not arrive.

---

## 2. The contract

### 2.1 Where the element comes from

The species row's `elementPrimary` / `elementSecondary`, the same source battle reads. **This module does
not invent an element for an actor that has none** — absent stays `Neutral`, which is a legal, meaningful
value (untyped, omni-only reads).

### 2.2 The change

`InjectorCombatBridge.ResolveActor` and `InjectorStatusBridge`'s sibling both pass `elementTypes:` into
`StatContextFactory.ForPlant` / `ForZombie`, constructed exactly as battle constructs it:

```
elementTypes: ActorElementTypes.Create(primary, secondary == primary ? null : secondary)
```

**Mirroring `BattleEngine.cs:36` verbatim is deliberate**, including the *"secondary equal to primary
collapses to null"* rule. Two constructions of the same concept that differ by a corner case is how the
two runtimes drift apart.

### 2.3 The existing debug pin stays, and stops being the only path

`InjectorElementOverride` is today the sole way any lawn actor gets a non-Neutral element. It remains a
debug override — **it must now override a real value rather than substitute for a missing one**, and its
precedence must be stated in code where it is read.

### 2.4 Where the species element is resolved from, on the lawn

The lawn's actors are Unity `Plant`/`Zombie` instances keyed by pointer, not species rows. **The lookup
path from a live entity to its species element is the one genuinely new piece of work in this module**,
and it must:

- resolve **once per actor per match**, cached — not per hit. **And this is a repair, not merely a
  precaution:** `ResolveElementTypesFromHub` already calls `InjectorBoardSnapshot.Capture()` and loops
  `board.Entities` on **every resolve** — it *is* the per-hit board scan the 2026-08 perf audit blamed.
  This module must leave that path faster than it found it, not merely avoid adding a second;
- return `Neutral` on a miss, never throw and never guess;
- be readable by both bridges without duplicating the resolution.

#### ⛔ DECIDED 2026-09-03 (owner removed themselves as a gate) — the algorithm

The three properties above were stated and no algorithm was. Here it is, in five steps, each one a call
that already exists:

```text
ElementOf(ptrKey) :=
  1. cache hit?                    -> return it.        cache key = (MatchKey, ptrKey)
  2. board lookup                  -> (side, gameTypeId)
  3. (side, gameTypeId) -> species -> DemonSpeciesDef
  4. def                           -> ActorElementTypes.Create(primary, secondary == primary ? null : secondary)
  5. miss at 2 or 3                -> ActorElementTypes.Neutral, reported once per (MatchKey, typeId)
  cache and return.
```

**Step 2 is a reuse, not a new scan.** `ResolveElementTypesFromHub` already captures a board snapshot
and loops `board.Entities` to find `side` and `typeId`
(`src/FusionRpg.Injector/Effects/InjectorCombatBridge.cs:51-59`) — on **every resolve**. This module
takes that existing loop and puts the cache in front of it, which is why §2.4 calls it a repair: the
per-hit board scan the 2026-08 perf audit blamed is the very line the element lookup needs, and caching
it removes the scan from the hit path for **both** the element resolve and the `side` the patron aura
rides on (`:33-34`).

**Step 3's key is `(Side, GameTypeId)`, and that pair is unique.** `DemonSpeciesDef` carries `Side` and
`GameTypeId` (`src/FusionRpg.Core/Demons/DemonSpeciesCatalog.cs:11-14`), and across the 84 shipped
species **no `(side, gameTypeId)` pair repeats** — checked mechanically against
`DemonSpeciesCatalog.Generated.cs`, 84 rows, 84 distinct pairs. `GameTypeId` alone is **not** unique
(`polevaulterzombie` and `wallnut` are both `3`), so a `typeId`-only key would silently give plants
zombie elements.

> **The roster is store-backed and can change.** `DemonSpeciesCatalog.All` reads what `species-import`
> wrote (`DemonSpeciesCatalog.cs:44-47`) and `Validate` enforces unique `SpeciesId` and unique
> `DemonTypeId` — **not** unique `(Side, GameTypeId)`. So the index this module builds must state its
> tie-break rather than assume one: **on a duplicate pair, take the lowest `SpeciesId` by ordinal and
> report the collision once at index build.** Deterministic beats arbitrary, and a reported collision
> is a roster defect someone can fix.

**Step 4 mirrors `BattleEngine.cs:36-38` verbatim**, and the collapse rule is belt-and-braces on this
path: `DemonSpeciesCatalog.Validate` already refuses `secondary == primary`
(`DemonSpeciesCatalog.cs:95-96`), so a species-sourced element can never hit it. **Write it anyway** —
§2.2's rule is that the two runtimes construct identically, and a corner case one side handles and the
other does not is how they drift.

**Where the cache lives and when it clears:** one `Dictionary<string, ActorElementTypes>` beside the
existing resolve, keyed on the normalised pointer, **cleared on `MatchKey` change**. A pointer is
match-scoped and can be reused by a different entity in a later match, so a process-lifetime cache
would hand a new zombie a dead plant's element. Test 5's counter asserts one resolve per actor per
match against exactly this.

**What would overturn it:** a shipped roster with a duplicate `(Side, GameTypeId)` that is legitimate
rather than a defect — at which point the key needs a third term (the board entity would have to carry
something more specific than `typeId`, which is a Unity-side question, not this one).

---

## 3. What this module must NOT do

- **Invent an element.** No element from the `typeId`, no default of `fire`, no round-robin. Absent is
  `Neutral`.
- **Change the matrices, the roster, or any channel.** All shipped and correct. This module supplies an
  argument.
- **Resolve per hit.** Cache per actor per match.
- **Diverge from battle's construction.** Same call shape, same corner case.
- **Write a Unity field.** Elements live in the RPG layer; nothing here touches `EntityStatWriter`'s
  surface.
- **Change `Neutral`'s meaning.** It stays the untyped case — omni reads only, elemental modifier zero.

---

## 4. Testing strategy

| # | Test | Proves |
|---|---|---|
| 1 | A plant of a species with `elementPrimary: "fire"` resolves **non-Neutral** in its `CombatActorSnapshot` on the lawn | The headline defect is closed |
| 2 | Same for a zombie, through `InjectorStatusBridge`'s path | Both bridges, not one |
| 3 | A species with **no** element resolves `Neutral` — no throw, no substitution | Absent is legal |
| 4 | `secondary == primary` collapses to `null`, **matching `BattleEngine.cs:36` exactly** | The two runtimes construct identically |
| 5 | Resolution happens **once per actor per match** — a counter asserts no per-hit resolve | The perf rule holds |
| 6 | **Planted violation:** a species with an **unknown** element id resolves `Neutral` and **reports**, rather than silently defaulting | An unparseable element is visible, not swallowed |
| 7 | An elemental resistance that was previously inert now changes overlay damage against a fire attacker | The 196 channels are actually live, not merely populated |

### ⛔ Sequencing hazards — both are open, owner-run, and read this exact path

- **VFX blind-identity trials.** `RequireElement` gates every burst and flash; *"plain/omni damage renders
  the number only"*. Today every lawn actor is Neutral, so those effects have never fired on the lawn.
  **After E27 they will.** Captures taken before and after are not comparable — **run the trials before
  E27 or after, never straddling.**
- **The shield live proof.** The gate resolves attacker/owner element through the same
  `InjectorCombatBridge.ResolveActor`, and the shield element matrix relation is currently always 0.
  Same rule: before or after.

**The injector is not built by CI.** This module needs a local build and an owner-run live check.

---

## 5. Acceptance criteria

1. A lawn plant and a lawn zombie both carry their species' element in `CombatActorSnapshot`.
2. Absent element → `Neutral`; unknown element → `Neutral` **plus a report**.
3. Construction matches `BattleEngine.cs:36` including the secondary-collapse rule.
4. Element resolution is cached per actor per match, asserted by a counter, and the cache clears on
   `MatchKey` change (§2.4).
4b. The `(Side, GameTypeId)` index is built once, and a duplicate pair is **reported** with a stated
   ordinal tie-break rather than resolved arbitrarily (§2.4).
5. An elemental defense channel measurably changes lawn overlay damage.
6. `InjectorElementOverride` still wins, and its precedence is stated where it is read.
7. No change to the element roster, either matchup matrix, or any channel id.

---

## 6. Dependencies and cross-program hazards

| | |
|---|---|
| **Depends on** | Nothing. May run first in Wave 7 |
| **Unblocks** | Every element-typed atom. **E30's pools are mostly element pools** — a pool resolving to `fire` is inert on a Neutral lawn, so this module is what makes L2 worth having there |
| **VFX** | Open blind-identity trials, `humanCorrect` still null — sequence around this module |
| **Shield** | Open live absorb proof reads the same resolve path — same sequencing rule |
| **class-system baselines** | `_baseline-residual.json` is measured **elements-live** (FORCE=fire / FINESSE=air / BASTION=earth). E27 turns elements on in the runtime those baselines describe; re-bless deliberately, not incidentally |
