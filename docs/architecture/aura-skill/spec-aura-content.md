# Spec: `aura-content`

**Program:** aura-skill · **Map:** [../aura-skill-map.md](../aura-skill-map.md) ·
**Ideal:** [../aura-skill-ideal.md](../aura-skill-ideal.md)
**Depends on:** `aura-action-shape`, `aura-magnitude`
**Status:** specced 2026-08-30, not built.

---

## 1. Objective

Author the **twelve auras** — one per aptitude — as `world-buff.*` containers, and deliver them to a
side through the shipped scope primitive.

This is the first real content of its kind: **no `world-buff.*` row has ever been authored**
(`buff-debuff-scope-ideal.md:114-122`), despite the enum member, the validator prefix and the store
round-trip all shipping. This module makes reserved plumbing real.

---

## 2. Container and grant are orthogonal — both, not either

Verified in code, and it settles map question Q6:

- A **container** is the content definition — `ContainerRow` (`ContainerRow.cs:37-39`) is *"mechanism,
  not content… what a skill contains — never when it fires"*: a named, ordered bundle of atom
  references.
- A **grant** is the delivery — a live binding of a compiled def to an owner scope.
- `AtomCompiler.EmitDefAndGrant` is the bridge, emitting **one `EffectDefDto` and one
  `EffectGrantDto`**. **Nothing in `ContainerKind` gates owner scope.**

So: **`world-buff.*` as the container**, delivered by an **ordinary battlefield scope grant** that
supports `Grant`/`WithdrawForOwner` at arbitrary times — which `aura-action-shape`'s toggle-and-evict
model requires.

⚠️ **`patron.aura` is not the model to copy.** Its grant is a **lifecycle marker only** — it carries no
overlay, and the magnitude lives in process-global static state (`PatronRuntimeState.MatchAura`) applied
by a bespoke injector overlay. It reaches the snapshot with **no provenance whatsoever**, which is
exactly the "unattributed producer" `spec-derived-stat-sheet.md:193-199` names (patron, stars, injuries,
contracts). Copying that shape adds a fifth and forfeits everything `derived-modifier-bucket` bought.

`ContainerKind` is a **closed six-member enum** (`ContainerRow.cs:3-15`) — `Item, Trait, Skill,
SpeciesPassive, Patron, WorldBuff`. This module adds no member; it authors rows under an existing one.

⛔ **But a `world-buff.*` container is not read by anything today.** `TraitAtomSource.FromContainers`
(`:61-63, 105-108`) — the one shipped consumer that turns containers into `BattleChannelMod`s —
**only accepts `ContainerKind.Trait` whose id matches `trait.<id>`**. A `world-buff.*` row is skipped
entirely.

This is the content-side face of **R4 / audit D5**: choosing the right container does not create a
reader for it. **`aura-delivery-path` owns making a `world-buff.*` container reachable**, and this
module cannot ship before it. An earlier draft of this spec treated delivery as *"add the aura kind
rows to `ScopeCompatibility` — a reviewed change"*, which understated it by a wide margin: that table
gates *delivery shape*, and adding a row there creates no executor, no trigger vocabulary, and no sink
arm.

---

## 3. Delivery — the shipped scope primitive

`WhereScope.Battlefield` × `ScopeHost.Live` × `WhoKind.Relation` with `RelationKind.Ally`
(`Scope/WhereScope.cs:9-13,42-46`, `Scope/WhoSelector.cs:10-16`, `Contracts/RelationKind.cs:11-17`).
Because `RelationKind` resolves **against the granter** rather than an absolute side, **one authored
row serves both factions** — Dave's Might aura and Zomboss's are the same content, mirrored.

Two shipped constraints this module must respect:

- **`WhoKind.Relation` never resolves by board scan** — `BattlefieldScopeExecutor.cs:41-43` throws:
  *"resolves via membership-events (T8), never a one-shot board scan."* Delivery is
  `BattlefieldOwnSideReactor`'s event-driven grant/withdraw. An aura's affected set is *"whoever
  currently holds a live per-entity grant from this source"* — never stored, never rescanned.
- **`ScopeCompatibility.Resolve` must return `PerEntityGrant`** or the reactor's constructor throws
  (`BattlefieldOwnSideReactor.cs:53-55`). The compatibility table is **four rows today**
  (`ScopeCompatibility.cs:48-70`) and anything unlisted raises `ScopeUnsupportedException`. **Aura
  kinds must be added to that table**, and that is a reviewed change.

---

## 4. The twelve auras

Each grants to `Ally` only; the "contests" column is the other side of the same differential and is
**not** a second grant (§4.1 of the ideal).

| Aura | Grants to `Ally` | Contests | Reads as | Gated by |
|---|---|---|---|---|
| Might | `combat.power` | `combat.defense` | your side hits harder | W3 |
| Fortitude | `combat.defense` | `combat.power` | your side takes less | W3 |
| Vigor | `combat.shield.capacity` | `combat.shield.pen` | your side is shielded | — **live** |
| Onslaught | `combat.block.break`, `combat.parry.break` | `block.rate`, `parry.rate` | their guard stops mattering | W3 |
| Agility | `combat.dodge` | `combat.accuracy` | hard to hit | W3 |
| Composure | `combat.crit.resist`, `.resist.damage` | `combat.crit.rate` | their crits stop landing | W3 |
| Pierce | `combat.shield.pen` | `combat.shield.capacity` | their shields stop mattering | — **live** |
| Bulwark | `combat.block.rate`, `combat.parry.rate` | `block.break`, `parry.break` | your side blocks | W3 |
| Retribution | `combat.reflect.damage` | `combat.reflect.resist.damage` ⚠️ | attacking you hurts | W4 |
| Precision | `combat.accuracy` | `combat.dodge` | never misses | W3 |
| Ferocity | `combat.crit.rate`, `combat.crit.damage` | `combat.crit.resist` | your side crits | W3 |
| **Focus** | ⟳ **reverses — see §4.1** | — | **the commander** acts more often | **D7** |

⚠️ **Channel names above are FAMILIES, not channels.** `combat.power` is a family
(`DerivedStatChannels.cs:188`); registered channels are
`combat.power.omni | .fire | .ice | .air | .earth | .light | .dark`, and `DerivedComposer.Compose`
calls `ValidateChannel` on every modifier — so **emitting on a bare family fails**. **Every aura writes
the `.omni` slot**, resolved below.

### Why omni — settled by arithmetic, not preference

An earlier draft called this *"a 7× magnitude decision."* **It is not a magnitude decision at all.**

`CombatDerivedReader.cs:9-51` reads **`omni + element(e)`, additively**, and `ElementPayload.Validate`
(`ElementPayload.cs:35`) enforces `Σ weights = 1.0`. `OverlayCombatCalculator.cs:143-145` states the
consequence itself: *"accumulating omni+element here, weighted, produces the same result as 'add omni
once' since weights sum to 1.0."* So:

| Write | Contributes |
|---|---|
| +X to **omni** | `Σ w_c · X` = **X** |
| +X to **all six elements** | `Σ w_c · X` = **X** — identical, at 6× the authoring cost |
| +X to **one element** | `w_e · X ≤ X`, and **exactly 0** when the attack has no component of that element |

(Six, not seven — `ElementTypeId` is Fire, Ice, Air, Earth, Light, Dark.)

Two further facts make element slots **broken**, not merely equal, for this feature:

1. **Parry, block and reflection are read omni-only.** `CombatDerivedReader.cs:53-72` — the per-element
   slots stay *"registered and unread"*. **Four of the twelve auras name exactly those families**
   (Onslaught, Bulwark, Retribution, and Composure's crit-resist pair). An element-slot version of
   those would be read by **nothing**.
2. **Untyped attacks resolve omni-only** (`OverlayCombatCalculator.cs:87-111`). An element-slot aura is
   inert against every untyped hit.

**`PatronAuraOverlay` is not a counter-precedent.** Its element *is* its content — `PatronPolicy.cs:5-6`:
*"per-mille combat bonuses on **the patron's element channels**."* The conditionality is the intended
flavour of a patron demon's identity. An **aptitude** has no element; Might and Bulwark are not
elements. The matching precedent is `BattleStatComposer.cs:8-11`: *"**level formulas fill the omni
halves**, element affinity fills the actor's own element channels."* An aptitude aura is universal.

**The table is *nearly* closed under opposition — and the exceptions are load-bearing.** An earlier
draft asserted closure absolutely and wrote test 2 against it. **That test fails on this data:**

- **Retribution contests `combat.reflect.resist.damage`, which no aura grants.** The channel is real
  (`CombatReflectResistDamageOmni`, read by `CombatDerivedReader.ReflectResistDamage`), so this is a
  genuine **content gap**, not a naming error.
- **Focus contests nothing**, deliberately — it reverses (§4.1).

Closure is what makes own-side-only work: two commanders running Might and Fortitude meet in one
contest and cancel, with no cancellation rule written anywhere. So the two exceptions must be
**explicitly exempted with a reason**, and **test 2 asserts closure over the non-exempt set**, not over
all twelve.

**Each aura names 1–3 signature channels, never its aptitude's whole edge list.** Three measured
reasons: the `kMilli` spread is **2200×**; distinctive counts run **3 to 12**; and **34 channels are
shared by all twelve** (5 `resource.max.*`, 5 `resource.regen.*`, 24 `status.*`), so including the tail
would make every aura ~70% identical. **The universal tail is out of scope** — which also makes W5
irrelevant to this program.

### 4.1 Focus reverses — it buffs the commander, not the units

**Owner decision, 2026-08-30:** *"focus primary? it should reverse for commander's other actions."*

Every other aura points **outward** — a side-wide grant to the units. **Focus points inward:** it
reduces the cooldowns of the **commander's own other equipped actions**. The commander acts more often;
the units are untouched.

Three things fall into place at once, which is the sign this is the right shape:

1. **It explains the anomaly in the data.** Focus is the only aptitude with **no opposed channel** — and
   now that is expected rather than awkward, because it is not a contest at all. It is self-tempo.
2. **It survives the substitutability test.** The economy-commander research asks: *"is your utility
   output substitutable by damage?"* Magic Find died because killing faster produced more loot.
   **Cooldowns on the commander's own actions buy *occasions*, which damage cannot buy** — the same
   reason Summoners War prices Attack Speed leads at 33% against 50% for raw stats and speed still
   defines the game.
3. **It removes the income trap.** `progression.xpRate` and `resource.efficiency` are excluded outright.
   *"Tempo bonuses are top-tier picks, income bonuses are the trap"* — a Creative Assembly forum post
   prices a recruitment-cost skill at ~3,600–5,400 gold per campaign and concludes *"every time I see
   it, I respec to remove it."*

**Targeting:** `RelationKind.Self`, not `Ally`. The scope primitive already has it
(`RelationKind.cs:11-17`), so no new vocabulary is needed.

#### The gate is smaller than it was, but real

Reversing does **not** make Focus live. `DerivedStatRegistry.cs:179` is explicit: *"No reader:
`CooldownMath.ApplyReduction` and `ActionEnvelope.CooldownChannel` both exist with zero callers — the
action/timeline layer that would wire them is unbuilt."*

But the gap is now **narrow and well-defined** — wire `CooldownChannel` into the action timeline for the
commander's own actions — rather than "the whole action layer, side-wide, on the lawn." Both stubs
exist and are waiting. Tracked as **D7** in
[derived-pipeline-audit-2026-08-30.md](../derived-pipeline-audit-2026-08-30.md).

#### The formula: divisive — and the "collides with shipped code" objection was wrong

> **Corrected 2026-08-30.** A previous revision of this section argued that mandating
> `newCD = baseCD / (1 + haste)` would *"replace determinism-guarded shipped code"*, and recommended
> keeping percentage reduction for v1. **All three premises of that argument are false.**

1. **There is no shipped behaviour to replace.** `CooldownMath.ApplyReduction` has **zero production
   callers** — every reference in the tree is a test. `DerivedStatRegistry.cs:179` and
   `catalog.json:546` both say so verbatim, and the method's own header reads *"No caller wired yet."*
   It is a stub, not shipped behaviour.
2. **The determinism guard does not forbid division.** The guard is
   `TimelinePurityGuardTests.Kernel_sources_contain_no_wall_clock_rng_or_floating_point` — a **source
   scan** for the tokens `DateTime`, `Random`, `double `, `float `. It bans floating-point **types**,
   not arithmetic. `baseCD * 1000 / (1000 + hasteMilli)` is pure `long` and passes trivially.
3. **Integer divisive haste already ships in the same directory, under the same guard.**
   `TurnReadiness.EffectiveRate` (`Battle/Timeline/TurnReadiness.cs:49-54`) is
   `speed * NominalHasteMilli / haste` with `NominalHasteMilli = 1000`, backed by the registered
   `turn.haste` channel, with a live consumer (`ReadinessDriver`) and tests.

**So the tradeoff runs the opposite way to how it was written: divisive *matches* the shipped
convention, and percentage reduction is the odd one out.**

| | Divisive (`×1000 / (1000 + hasteMilli)`) | Percentage reduction |
|---|---|---|
| Marginal value | **constant** | hyperbolic — each point worth more than the last |
| Needs a cap | **no** — this is exactly why Riot deleted its 40% cap after switching | **yes**, to contain the exponential |
| Determinism guard | passes (pure `long`) | passes |
| Repo precedent | **`TurnReadiness.EffectiveRate`, same directory** | none live |

**Decision: divisive.** Roughly fifteen lines — rewrite `ApplyReduction` (or add a sibling) as
`Math.Max(MinTicksFloor, RoundDivSigned(baseCD * 1000, 1000 + hasteMilli))`, keeping `MinTicksFloor`
as the structural anti-infinite-loop bound it already is (PS-8-exempt, and correctly documented as
such at `CooldownMath.cs:13-27`).

---

## 5. Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~AuraContent
dotnet test tests\FusionRpg.Core.Tests
dotnet run --project tools\ItemSeedValidator   # if it validates container rows
python scripts\audit-magic-numbers.py --targets M1
```

---

## 6. Project structure

| Path | Change |
|---|---|
| `data/seed/containers/aura.json` | **new** — twelve `world-buff.aura-*` rows |
| `data/tuning/aura.v1.json` | edit — per-aura splits |
| `src/FusionRpg.Core/Scope/ScopeCompatibility.cs` | edit — add the aura kind rows (reviewed change) |
| `tests/FusionRpg.Core.Tests/Actions/Aura/AuraContentTests.cs` | **new** |

---

## 7. Testing strategy

| # | Test | Asserts |
|---|---|---|
| 1 | All twelve rows load | valid `world-buff.*` ids, pass `ContainerValidator` |
| 2 | Opposition closure **over the non-exempt set** | every contested channel is another aura's granted channel, **excluding the two declared exemptions** (Retribution's unbacked `reflect.resist.damage`, Focus's reversal). The exemption list is itself asserted — adding a third without a reason fails |
| 3 | Signature size | every aura names 1–3 channels; none references the 34-channel universal tail |
| 4 | Budget conservation | each aura's splits sum to `budgetMilli` |
| 5 | `Ally` only | no aura emits an `Enemy`-relation grant — the own-side-only guard |
| 6 | One row, both factions | the same row grants correctly for a plant granter and a zombie granter |
| 7 | Scope compatibility | every aura kind resolves to `PerEntityGrant`; an unlisted one throws |
| 8 | Focus excludes income | its splits reference **no** `xpRate` / `resource.efficiency` channel |
| 9 | Gated auras degrade honestly | with `OVERLAY-COMBAT` off, a W3-gated aura is inert but does not throw |
| 10 | Round-trip | rows survive `RpgStore.Containers` save/load unchanged |

**Test 2 is the structural guard** — it is what keeps the own-side-only design coherent as content
changes, and it is cheap because the table is data.

---

## 8. Boundaries

**Always**
- Author under `world-buff.*`; grant to `Ally` only.
- Keep splits in tuning.
- Name the real reason a gated aura is inert.

**Ask first**
- Adding rows to `ScopeCompatibility` (a reviewed change).
- Any aura touching the universal tail.
- Authoring Focus's income channels.

**Never**
- Add a `ContainerKind` member.
- Copy `patron.aura`'s unattributed-overlay shape.
- Resolve `Relation` by board scan.
- Use percentage cooldown reduction.

---

## 9. Success criteria

- [ ] Twelve `world-buff.aura-*` rows load, validate and round-trip.
- [ ] Opposition closure holds as a test.
- [ ] No aura grants to `Enemy`; one row serves both factions.
- [ ] Focus is cooldown-only, divisive form.
- [ ] Gated auras are inert-and-honest, never crashing.
- [ ] Magic-number audit clean.

## 10. Open questions

1. **Does Focus ship at all in v1**, or wait for the action layer on the lawn? R1 is a real gap and no
   design removes it. Recommendation: author the row, mark it inert-with-reason, ship it dark.
2. **The `commanderOnly` item role is a second, unacknowledged answer** to "how does the commander buff
   the squad" — one slot, match owner-scope, whole-squad reach, its own 100‰ budget, never authored.
   Whether banner atoms and aura atoms stack, and against which budget, is undecided anywhere. **Owner
   call; not blocking.**
