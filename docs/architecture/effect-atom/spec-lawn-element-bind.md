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
4. Element resolution is cached per actor per match, asserted by a counter.
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
