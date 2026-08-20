# Chaos combat and element adaptation for FusionRpg overlay damage

Date: **2026-08-19**. Research bridge for adapting Chaos `element-core` and `combat-core` ideas into FusionRpg.

This is a **design reference**, not a product ADR. Normative specs live in:

- [../../architecture/element-hub-ssot.md](../../architecture/element-hub-ssot.md)
- [../../architecture/combat-damage-ssot.md](../../architecture/combat-damage-ssot.md)
- [../../architecture/actor-hub-ssot.md](../../architecture/actor-hub-ssot.md)
- [../../architecture/status-ssot.md](../../architecture/status-ssot.md)
- [../../architecture/combat-element-implement-plan.md](../../architecture/combat-element-implement-plan.md)

External source docs reviewed on the local machine:

- `D:/Works/source/chaos-repositories/chaos-backend-service/docs/element-core/20_Unified_Architecture_Design.md`
- `D:/Works/source/chaos-repositories/chaos-backend-service/docs/element-core/09_Actor_Core_Integration_Guide.md`
- `D:/Works/source/chaos-repositories/chaos-backend-service/docs/element-core/06_Implementation_Notes.md`
- `D:/Works/source/chaos-repositories/chaos-backend-service/docs/element-core/11_Advanced_Derived_Stats_Design.md`
- `D:/Works/source/chaos-repositories/chaos-backend-service/docs/element-core/01_Probability_Mechanics_Design.md`
- `D:/Works/source/chaos-repositories/chaos-backend-service/docs/element-core/16_Hybrid_Subsystem_Design.md`
- `D:/Works/source/chaos-repositories/chaos-backend-service/docs/combat-core/damage-management/01_Damage_Manager_Core_Design.md`
- `D:/Works/source/chaos-repositories/chaos-backend-service/docs/combat-core/11_Damage_Application_Engine.md`

---

## 1. FusionRpg goal is narrower than Chaos

Chaos is built like a broader RPG substrate:

- actor-core style data hubs
- element mastery progression
- many advanced combat stat families
- hybrid elements, tags, and conditional modifier packs
- shields, protections, and resource distribution layers

FusionRpg does **not** need all of that for this version.

The local product goal is smaller:

- keep vanilla PVZ attack and hit logic intact
- add an **RPG overlay combat layer** beside it
- compute a final overlay HP delta from derived combat + element data
- send that delta into the already-built injector path
- keep timed status logic separate in the existing status runtime

---

## 2. What we copy directly

### 2.1 Data-hub shape

Chaos element-core treats element as a **data hub** fed by contributors and consumed by combat. That pattern ports well.

Fusion mapping:

| Chaos concept | FusionRpg mapping |
|---|---|
| Unified registry | `ElementHub` doc-owned element roster + matchup matrix |
| Contributor pattern | Actor Hub subsystem contributors / future PvzStats or progression rows |
| Aggregated per-actor view | `ActorDerivedSnapshot` + actor type metadata |
| Consumer systems | overlay combat layer only in v1 |

### 2.2 Omni additive-only rule

Chaos `06_Implementation_Notes.md` is explicit: omni is additive baseline only.

Fusion keeps that rule everywhere:

```text
total = omni + typed
```

Never:

```text
total = omni × typed
```

This already matches local status design and is now extended to combat and element.

### 2.3 Sigmoid probability family

Chaos uses sigmoid curves for probability-based stats. Fusion keeps that family for:

- hit / miss
- crit chance
- crit magnitude shaping

But Fusion does **not** port Chaos status probability families into overlay combat v1, because local status already has its own Apply-time math.

### 2.4 Hybrid payload idea

Chaos hybrid subsystem shows that multi-element payloads scale better when represented explicitly, not hidden behind one dominant type.

Fusion keeps the structural idea:

- a hit may carry multiple weighted element components
- defender may have up to two concrete types
- resolve **per-component** matchup bonuses, then weight-sum in overlay combat

But Fusion reduces the result to a single additive matchup bonus instead of a separate hybrid subsystem with tags and modifier packs.

**Locked v1 combine rules:**

- dual-type defender: product of slot multipliers (Pokemon-style), then convert to additive share
- hybrid payload: `matchupBonus = Σ (weight × componentBonus(E))`
- matrix: 4-element ring cycle — see [element-hub-ssot.md §8.5](../../architecture/element-hub-ssot.md)

---

## 3. What we adapt heavily

### 3.1 Element roster

Chaos supports a much larger and more fluid element space. Fusion locks a tight v1 roster:

- `omni`
- `fire`
- `ice`
- `air`
- `earth`

Reason:

- easier balance pass
- fits current project scope
- enough room for matchup design and hybrid payloads
- avoids shipping a large unused taxonomy

### 3.2 Actor typing model

Chaos talks about element mastery, tags, and hybrid activation. Fusion instead uses simple actor typing:

- each actor has 0 to 2 concrete element types
- `omni` is not an actor slot
- element types are metadata, not derived channels

That is closer to a clean tactics/RPG overlay model than Chaos cultivation structure.

### 3.3 Matchup output

Chaos often feeds interaction results into effects, dynamics, or modifiers.

Fusion v1 adapts matchup into a smaller contract:

```text
componentBonus(E) = relationShare(E, defenderTypes) × baseOverlayDamage
matchupBonus = Σ (weight × componentBonus(E))
```

Policy: **`MatchupShareK = 0.25`** — strong = +0.25× base, weak = −0.25× base, neutral/same = 0.

Ring cycle: `fire → ice → earth → air → fire` (canonical table in element-hub-ssot §8.5).

Not in v1:

- final-damage multiplier applied after crit
- status hook trigger
- refractory or intensity dynamics
- conditional tags or aura packages
- `PowerScale` inside matchup (independent from typed power/defense)

This keeps the first implementation inspectable and debug-friendly.

### 3.4 Combat scope

Chaos combat-core is closer to a full damage engine with shields, protections, and typed resource distribution.

Fusion trims that to:

- target already resolved
- compute hit / crit / delta
- produce final signed HP delta
- feed Funnel -> FA10 Writer Add

That means the right local analogue is not a full combat-core port. It is a **small overlay damage calculator** that sits above the injector apply path.

---

## 4. What we explicitly defer

### 4.1 Chaos element status families

Deferred in Fusion v1:

- `StatusProbability`
- `StatusResistance`
- `StatusDuration`
- `StatusDurationReduction`
- `StatusIntensity`
- `StatusIntensityReduction`

Reason:

- local `status-ssot.md` and `actor-hub-ssot.md` already define the status-derived vocabulary
- duplicating it under combat/element would create two competing status systems

### 4.2 Advanced combat defenses

Deferred in Fusion v1:

- `ParryRate`
- `ParryBreak`
- `ParryStrength`
- `ParryShred`
- `BlockRate`
- `BlockBreak`
- `BlockStrength`
- `BlockShred`

Reason:

- too wide for the first overlay combat pass
- local apply path already needs a clean power / defense / hit / crit foundation first

### 4.3 SMT-style elemental reactions

Deferred in Fusion v1:

- penetration
- absorption
- reflection
- null / drain / repel style outcomes

Reason:

- the user explicitly reverted these for future versions
- they complicate final-delta reasoning and test coverage early

### 4.4 Mastery, economy, mobility

Deferred in Fusion v1:

- mastery progression systems
- leadership / teaching / crafting stats
- movement / teleport / healing side systems
- terrain-sensitive element rules

Reason:

- not needed to ship the overlay combat calculator
- does not fit the current PVZ overlay scope

---

## 5. Final v1 stat surface

Fusion v1 keeps only 4 combat stat families, each with `omni + fire + ice + air + earth`:

| Family | Attacker | Defender |
|---|---|---|
| Base delta | `combat.power.*` | `combat.defense.*` |
| Crit chance | `combat.crit.rate.*` | `combat.crit.resist.*` |
| Crit magnitude | `combat.crit.damage.*` | `combat.crit.resist.damage.*` |
| Hit / miss | `combat.accuracy.*` | `combat.dodge.*` |

That is the whole v1 combat-derived vocabulary.

Why this cut is good:

1. It is enough to make overlay damage feel typed and RPG-like.
2. It reuses the Actor Hub pattern instead of inventing a second stats system.
3. It leaves room to add advanced Chaos families later without invalidating the first design.
4. It stays easy to prove with deterministic tests.

---

## 6. Chaos formula borrow vs Fusion divergence

### Borrowed

- additive omni + typed slices
- sigmoid probability family
- explicit element component payloads
- matrix-driven relationship thinking
- derived-only combat inputs

### Diverged

| Topic | Chaos | Fusion v1 |
|---|---|---|
| Product scope | broad RPG substrate | overlay damage layer only |
| Element count | wide / extensible | 4 concrete + `omni` |
| Status coupling | shared element-status vocabulary | status stays independent |
| Matchup output | can feed effects and dynamics | base-damage share (`MatchupShareK=0.25`), ring-cycle matrix |
| Hybrid resolution | dedicated subsystem / tags / activation | per-component bonus + weighted sum; dual-type product |
| Per-element sigmoid tuning | distinct config values | same shared scales; shape documented only |
| Resource application | shields / protections / split resources | final HP delta only |

---

## 7. Recommended implementation reading order

For future code work in FusionRpg, the most relevant reading order is:

1. [../../architecture/actor-hub-ssot.md](../../architecture/actor-hub-ssot.md)
2. [../../architecture/element-hub-ssot.md](../../architecture/element-hub-ssot.md)
3. [../../architecture/combat-damage-ssot.md](../../architecture/combat-damage-ssot.md)
4. [../../architecture/combat-element-implement-plan.md](../../architecture/combat-element-implement-plan.md)
5. [../../architecture/effect-funnel.md](../../architecture/effect-funnel.md)
6. [../../architecture/status-ssot.md](../../architecture/status-ssot.md)
7. [../../research/actor-core-chaos-mapping.md](../../research/actor-core-chaos-mapping.md)
8. [../../research/status-core-chaos-mapping.md](../../research/status-core-chaos-mapping.md)

That keeps the product docs in charge and Chaos as reference only.

---

## 8. Bottom line

Chaos gave FusionRpg three good patterns worth keeping:

1. **derived stats as the combat input layer**
2. **omni as additive baseline, not multiplier**
3. **explicit matrix logic for typed interactions**

Everything beyond that is trimmed hard for this version.

FusionRpg v1 therefore becomes:

- smaller roster
- simpler formulas
- independent status runtime
- no mastery port
- no advanced defensive side systems
- one clean output: final overlay damage sent to the injector

