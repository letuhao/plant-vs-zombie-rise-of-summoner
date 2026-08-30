# Aura skills — the ideal

**Status:** idea phase, 2026-08-29. **Not a spec. No build authorized.** This is the "later discuss"
conversation that `buff-debuff-scope-ideal.md` §5 and `decisions.md:103` deferred by name when the
scope primitive shipped.

**Owner framing, verbatim, 2026-08-29:** *"the aura works on the lawn but it is RPG layer, not PvZ
game engine — we already design everything on the RPG layer and this is a rule"* · *"we can add
buff/debuff all plant/zombie by % for both side"* · *"problem is only what we should buff/debuff and
how to tunable them."*

---

## 0. The principles this design stands on, stated here rather than linked

A downstream session reads this document, not its links. These are restated inline on purpose.

1. **Every RPG feature lives in the RPG layer. It is never built by changing what PvZ is.** PvZ's
   narrow Unity write surface (`EntityStatWriter` — 5 plant fields, 8 zombie fields) constrains
   exactly one thing: what a *persistent vanilla stat change* can touch. It says nothing about what an
   aura may do. Aura effects resolve through the RPG's own stack — `DamagePacket` →
   `CombatDamageDispatcher` → `ShieldGate`/`OverlayCombatCalculator` → Funnel → FA10 — all of which
   run during a live lawn match and none of which require PvZ to have heard of crit, shields, dodge
   or elements.
2. **Two async systems.** The RPG observes past events and contributes signed deltas. It never reads
   or guesses PvZ's current state. Record-then-drain; delay is the designed degradation mode.
3. **One power ladder.** Contests read `Θ` (linear, difference-based); magnitudes read `P(Θ)`
   (`ssot-power-scale.md` PS-3). §10's inventory is closed. **This design reads the existing ladder
   and introduces no new `f(level)`** — which is what keeps it inside that closed inventory.
4. **The balance surface is data.** Every number here belongs in `data/tuning/aura.v1.json`. No
   `const` on the balance surface.
5. **No hard progression ceilings.** Soft caps are configurable; absolute bounds throw, never clamp.
6. **A wiring gap is not an architectural wall.** A default-off toggle, a null delegate, a debug-only
   entry point, or an argument omitted at every call site is unfinished wiring. Say so with that word.

> **Incident that produced rule 1 and rule 6 (2026-08-29).** The first pass at this feature read the
> injector's Unity write surface, found `progression.bonus.defense` never reaches a Unity field and
> that plants have no armour fields, and concluded *"the lawn can express about five of the twelve
> aptitudes."* Wrong frame, wrong number. The aura is an RPG-layer effect and never needed a Unity
> field; every blocker found was unfinished wiring. The corrected count is **11 of 12**. This is now
> written into `CLAUDE.md` as an explicit architecture rule.

---

## 1. What an aura is

**An aura is an earned skill that, while enabled, continuously buffs one derived channel across your
own whole side of the board.** It occupies one of the five equipped-skill slots, and it costs resource
every tick it stays on. Nothing about it is free.

There are **eleven or twelve auras, one per aptitude** — the aptitude tells you what the aura *means*,
and two axes decide how strongly. This is the Heroes-of-Might-and-Magic-III model the owner named: the
commander is not a unit on the board, and their stats reach the fight by lifting everything they
command.

> ⚠️ **Corrected 2026-08-30 — this section previously said an aura "debuffs the opposed channel across
> theirs," and described a "buff half and debuff half."** That was the pre-decision draft. **Owner
> decision Q7 chose own-side-only** (§4.1 option A): an aura issues **one grant, to `Ally`**. The enemy
> is affected through the *contest differential*, not a second grant — which is what all five surveyed
> off-field-commander games do, and what avoids shifting one differential twice. Anyone reading only
> this section would have built the wrong feature.

Two properties follow from the design and are worth stating up front:

- **One grant, both sides affected.** Because the twelve aptitudes already come in opposed pairs, an
  aura granted to your own side *is* the other end of a contest the enemy is already reading. The
  "debuff" is emergent, not authored.
- **Symmetric by construction.** Zomboss runs auras from the same twelve, drawn from the same finite
  point pool. A harder Zomboss is a higher `Θ` or a better allocation, never a stat nobody could have
  had (`spec-zomboss-patterns.md:43-45`). ⚠️ **No module owns this yet** — Zomboss has no `qi` pool, no
  loadout, and no `playerId` for `ScopeKey`. Until that exists the aura is a one-sided buff with no
  counterplay, which is the failure mode §3.4.2 warns about.

---

## 2. What already exists

Sorted into **built** / **wiring gap** / **real gap**. This inventory is the most load-bearing part of
this document: most of this feature is already in the tree.

### 2.1 Built

| Thing | Evidence |
|---|---|
| **Five equipped-skill slots** | `LoadoutSet.MaxSize = 5` (`Loadout/LoadoutSet.cs:40`); named as the real bottleneck by `Grants/CapPolicy.cs:38`. Only `Skill`-kind costs a slot — `Basic`/`Innate` are intrinsic |
| **Per-tick cost machinery** | `ActionCostTiming.PerTick` (`ActionEnums.cs:67`); `CostLedger.TryPay` validates all rows then consumes all, never partially (`Cost/CostLedger.cs:106-138`); failing to pay ends the action through the interrupt path |
| **Per-tick cost is priced structurally** | Any `PerTick` row spends the `consumption` structure axis; an action whose rung does not budget it is **rejected at load** (`StructureBudgetGuard.cs:67`) |
| **A toggled, continuously-draining action** | Guard — `Actions/Defence/StanceRuntime.cs`. "Held" is a plain per-actor dictionary entry with `BaseDuration: 0`; no new FSM state, no runtime of its own |
| **The scope primitive — "who does this reach"** | Shipped 2026-08-29 (`decisions.md:103`). `WhereScope{Battlefield,WorldMap}` × `ScopeHost{Sim,Live}` (`Scope/WhereScope.cs:9-13,42-46`), `WhoKind{Target,Type,UniqueDemon,Relation}` (`Scope/WhoSelector.cs:10-16`), `RelationKind{Self,Ally,Enemy,Any}` (`Contracts/RelationKind.cs:11-17`). **`Battlefield × Live × Relation` is exactly "all plants / all zombies, both sides, on the lawn."** Delivery is event-driven grant/withdraw, never polled |
| **A working side-wide aura in production** | `patron.aura` — a match-owner effect grant carrying `{element, powerMilli, defenseMilli}`, Secondary plugin → Funnel → `PatronAuraOverlay.cs:22-27`, applied at `board.start`, withdrawn at `board.end` |
| **Continuous-while-granted needs no new atom** | `stat.modify` / `stat.derived` declare **no trigger at all**; apply/revert is a runtime lifecycle mechanic (`effect-atom/definitions.md:719-721`). Continuity comes from grant lifetime, not an atom kind. `fx.patron_aura` is the shipped precedent for a passive with an empty trigger list (`:732`) |
| **Periodic re-assertion needs no new mechanism** | Shield auras ride the existing `OnTimer` trigger — *"No new 'trait pulse' mechanism"* (`shield-system-spec.md:151`) |
| **12 aptitudes, 486 edges, 84 channels** | `Stats/Aptitudes/Aptitude.cs:28-52`; `data/tuning/aptitudes.v2.json` (counted directly, not read off the header) |
| **Commander is already an allocation scope** | `AllocationScope.Commander` (`AptitudeAllocation.cs:8`), weighted **smallest** of four on purpose — a commander allocation replicates across the whole roster, so a dominant one is the worst case (`decisions.md:101`) |
| **Dave's level is already the ladder's main line** | `Θ_actor = Wd·daveLevel + Wa·realmsAdvanced + Wr·runTerm(pvzRuns)`, `Wd = 1000‰` — the unit every other axis is expressed against (`ssot-power-scale.md:229,238,296`) |
| **Zomboss already has authored allocations** | Nine named patterns, `Battle/Ai/ZombossPatterns.cs`, `data/seed/zomboss/patterns.json` |
| **Aura already has a shield drain priority** | `aura 30 → skill 20 → innate 10`, and aura re-asserts are idempotent (`decisions.md:41`) |
| **A reserved home for commander aura content** | `ContainerKind.WorldBuff` — the enum member, the `world-buff.*` validator prefix, and the store round-trip all ship. **Nothing has ever authored a row** (`buff-debuff-scope-ideal.md:114-122`), which names a commander aura as its intended first tenant |

**The atom vocabulary needs no addition.** 5 attach points, 12 kinds, 8 triggers (`Effects/Atoms/AtomKind.cs`
— note `OnActivate` is the eighth, added by reviewed spec; `DESIGN-GATE.md:40` still says 7 and is stale).
An aura is expressible today as a container of `stat.derived` atoms under a scope.

### 2.2 Wiring gap — inert machinery, not missing machinery

Each of these is a specific line that is switched off. **None is an architectural limit.**

| # | Gap | The inert line | Consequence |
|---|---|---|---|
| **W1** | Commander allocation never reaches the injector | `Injector/CheatState.cs:43-44` builds the `ActorHub` with no `aptitudeAllocation` delegate → `AptitudeSubsystem.cs:43` falls back to `AptitudeAllocation.Empty` | **All 486 aptitude edges evaluate to zero on a live lawn.** The subsystem's own comment calls itself *"wired into production and provably inert"* |
| **W2** | `Θ` is never hydrated on the lawn | `Injector/Stats/InjectorPowerIndexProvider.cs` — its own comment: *"No such source exists yet… `ActorIndex` therefore returns 0 for every context"* | At `Θ = 0` every magnitude collapses to `P(0) = C` — the same floor for every build. Reads as a coefficient bug and is not one |
| **W3** | Overlay combat math is default-off | `Injector/Effects/OverlayCombatFeature.cs:13` — `Enabled => EnvEnabled \|\| CheatState.On(CheatToggleId)`. No production default turns it on; only two probe scripts | `power/defense/accuracy/dodge/crit/parry/block/penetration/absorption/amplification/reduction` are all read by `OverlayCombatCalculator`, and all inert while this is off |
| **W4** | Reflect never runs in production | `CombatDamageDispatcher.cs:70` guards `TryReflect` on an `actorResolve` argument **every production call site omits** (`EffectBag.cs:479,547`, `StatusEffectBridge.cs:77,120`, `CheatCommandRunner.cs:1303` all stop at `shieldGate`); only the offline harness passes it | Retribution's entire identity (`combat.reflect.*`) is inert. The math exists and is exercised in tests |
| **W5** | Lawn status bypasses the status runtime | FA2 `ApplyStatus` calls `DebugActions.ApplyStatusToZombie` directly (`InjectorEffectActionSink.cs:209,218,241`) — a raw Unity CC adapter, 8 statuses, zombies only. `StatusRuntime` (which reads all 24 `status.*` channels via `ResistanceEvaluator`) is mounted and ticked but reachable only from `debug.status.apply` | The 34-channel universal tail is unreadable on the lawn. **Not needed for this design** — see §4.3 |
| **W6** | `progression.bonus.defense` is composed but never written | Neither `WritePlant` nor `WriteZombie` touches `EntityFinal.DefenseFlat` | Affects only the *vanilla stat bleed* path (§4.5), never the aura path — `combat.defense.*` is a different channel with a live reader |

**W1 and W2 are the whole HoMM3 half.** They are also the highest-value fix in this document: until
they land, every aura magnitude that scales on commander investment is multiplying by zero.

### 2.3 Real gap

| # | Gap | Evidence |
|---|---|---|
| **R1** | `skill.cooldown.*`, `skill.effectiveness.*`, `resource.efficiency.*`, `move.range`, `progression.xpRate`, `progression.breakthroughSuccess` have **no reader anywhere** | `scripts/audit-reader-census.py`: 48 families, 42 with a reader, 6 without; 18 of 486 edges (3.7%). `aptitudes.v2.json:9` says so itself — *"the action layer is not built — their coefficients here are DESIGNED, not measured, and must not be reported as balanced"* |
| **R2** | No aura content has ever been authored | No `world-buff.*` row exists; `concrete-action-roster.md` is reference prose explicitly marked *"Never imported"* |
| **R3** | The commander concept itself is undecided | Crazy Dave and Dr. Zomboss exist **only** as world-map faction ids (`WorldTemplateCatalog.cs:56,78`, `FactionKindCatalog.cs:26`) and two power-index scalars. Neither is an actor, battle entity, or equippable thing anywhere in `src/` or `data/` |

| **R4** | **`stat.derived` does not run on the lawn** — runtime matrix `RuntimeSupportMatrix(None, Full, None)` (`AtomKindRegistry.cs:145`), and its one consumer (`TraitAtomSource.cs:55-90`) never reads `op`. Meanwhile `stat.modify` is **rejected at bind** for any non-primary channel (`AtomKindRegistry.cs:64-72`) | **Neither atom road delivers a `combat.*` aura to a lawn entity today.** Found 2026-08-30; see §4.4a |
| **R5** | **The derived pipeline has no per-source bucket.** `ActorDerivedSnapshot` is `Dictionary<string,double>`, so provenance is destroyed at `DerivedComposer.Compose`; `ActorHub` recomputes *"fresh per call in v1"* (`ActorHub.cs:51`) | The primary pipeline (`ModifierBag`) has full provenance; the derived side has none. This is the gap the owner's Q7a bucket ask targets — §4.4a |

**R1 is why Focus is the one aptitude with no aura in §4.2.** Its identity *is* cooldowns and resource
efficiency.

### 2.4 Two defects found while investigating — not gaps, hazards

- **D1 — a percentage on `combat.*` composes to nothing.** Every `combat.*` channel is registered
  `DerivedComposeKind.FlatSum` (`DerivedStatRegistry.cs:249`), and `FlatSum` sums **only `Flat` ops**
  (`DerivedComposer.cs:42`). An `Increased` modifier on `combat.power.omni` is **silently dropped** —
  no error, no log. A percentage aura needs a compose-kind change or a Flat formulation.
- **D2 — `ActorDerivedSnapshot.Overlay` is replace, not add** (`ActorDerivedSnapshot.cs:47-53`).
  `PatronAuraOverlay.cs:37` compensates by hand. **A second overlay written naively silently erases the
  first.** No arbitration, no guard test. The sharpest implementation hazard in this document.

Both are pre-existing and independent of auras; both would bite an aura implementation immediately.

---

## 3. Prior art

### 3.1 Heroes of Might and Magic III — the commander model

- **The hero is never on the grid.** Off-field portrait, one spell per round, cannot be targeted, cannot
  die. Their contribution to the fight is entirely through the stacks they command.
- **Hero Attack/Defense are flat additive terms folded into each unit's own stat**, then damage is
  decided by the *difference*: attacker ahead → `+5%` damage per point, capped at **+300%** (a 60-point
  lead); defender ahead → `−2.5%` per point, capped at **−70%** (a 28-point lead).
- **Because it is a flat add against a difference, the same hero bonus is worth the same percentage to
  a cheap unit as to an expensive one** — `+10` Attack is `+50%` damage whether the unit's base attack
  is 4 or 27. This is precisely the "my whole lawn feels my commander" effect.
- Equal heroes cancel, and the fight reverts to unit stats — **self-balancing against an enemy
  commander**.
- Bonuses live in an additive bracket; each *reduction* source gets its **own multiplicative bracket**,
  so stacked reductions approach but never reach 100%. Anti-stacking with no special-case rules.
- Level-ups award primary stats **randomly, weighted by class**, flattening at level 10+. (We diverge:
  our allocation is player-chosen. Noted deliberately, not by accident.)

Sources: [thelazy.net/Damage](https://heroes.thelazy.net/index.php/Damage) ·
[Primary_skill](https://heroes.thelazy.net/index.php/Primary_skill) ·
[Hero_class](https://heroes.thelazy.net/index.php/Hero_class) ·
[homm.fandom.com/wiki/Combat](https://homm.fandom.com/wiki/Combat)

**Why this matters to us:** HoMM3's contest is a *linear difference*, which is exactly what PS-3
mandates (*"contests read `Θ` — linear, difference-based"*) and for exactly the same reason: a
geometric curve makes a fixed level gap unboundedly decisive. **The HoMM3 model is not a foreign
import; it is the shape our own ladder already requires.**

### 3.1a Team-wide percentage auras — what shipped games actually authorize

Surveyed: Summoners War, Raid: Shadow Legends, AFK Arena, Brave Frontier, Fire Emblem Heroes,
Arknights, TFT, Dota Underlords.

**The single most important finding for us — auras apply to the BASE stat, not the total.** Summoners
War: *"calculated from the base stat of each individual monster."* Raid, explicitly excluding gear and
account bonuses: *"a 19% speed aura is applied over champion's base speed, not his max speed."*

This is the mechanism that stops a permanent army-wide percentage from compounding. Applied to a base
quantity the aura is **additive with gear**; applied to totals it is a multiplier stacked on every
other multiplier. **It matters more for us than for them, because `P(Θ)` is already quadratic** — an
aura multiplying derived stats would multiply a quadratic by a percentage that itself scales with
commander investment, which is the compounding path straight into the overflow thresholds `CLAUDE.md`
documents. Warcraft 3 shipped the same guard in one word: Trueshot Aura reads *"base ranged damage"*,
so it never multiplies upgrades or items.

Corroborating: Summoners War has **no documented leader-skill nerf in its history** — base-stat
application, a single leader slot, no investment scaling and persistence-after-death appear to make
the architecture structurally not need one.

**Restriction is the currency you spend to buy magnitude.**

| Scope | Shipped magnitude |
|---|---|
| Permanent, army-wide, no conditions (FEH Legendary blessing) | **~5%** |
| Unconditional, all content (Raid / Summoners War) | **15–35%** |
| Restricted to one element or one game mode | **50–55%** |
| Conditional burst (Brave Frontier, 2 turns only) | 140–150% |

**Nobody ships a 50% unconditional army-wide buff.** Every 50%+ figure in the dataset carries an
element, area, turn-count or threshold gate. An always-on, all-units, all-content aura belongs at the
*low* end of that table.

**Stacking is solved by exclusivity, not arithmetic.** Four of seven surveyed games never answer "how
do two auras combine" because a single leader slot makes it impossible. FEH is the one that answers it
head-on, and its rule is the cleanest general form available: **persistent + positional ⇒ highest-only;
conditional + in-combat ⇒ additive.** (FEH's own failure — "horse emblem" — came from blade tomes
*reading the sum of active buffs*, which converted an additive system into a multiplicative one. A
mechanic that sums buffs defeats a highest-only rule.)

**Both Summoners War and Raid keep the aura active after the leader dies** — deliberate anti-degeneracy,
so "focus the commander" never becomes the only opening move. Our commander is off-board, so we get
this property for free.

### 3.1b Enemy-side debuff auras are rare — and the reason bites us

Absent entirely from Summoners War, Raid, AFK Arena, Brave Frontier and Epic Seven. Present only in
symmetric auto-battlers (Underlords: Heartless −15 Armor, Mage −100% MR) and in FEH, where they are
gated behind positioning *and* a stat check.

No developer statement explaining the avoidance was found. The most credible reading (analysis, not
sourced): enemy stats are the tuning reference frame, so a permanent enemy debuff means every
encounter is tuned twice; it collides with the accuracy/resistance economy; and it is **asymmetrically
useful** — a −30% defence aura is enormous against armoured content and worthless against squishy
content, which is a worse balance surface for the same nominal power.

**That last point lands on us directly: PvZ zombies vary enormously in armour**, so a debuff on
`combat.defense` would be far swingier across a run than the equivalent ally buff.

### 3.2 Aura cost models, and each one's documented failure

| | Reserve pool (PoE) | Drain per tick (GW1) | Slot only (D2/WoW) |
|---|---|---|---|
| Decision made | build time, once | continuously, in play | build time, once |
| Failure mode | balloons if the cost reducer is subtractive | bookkeeping tax | **becomes always-on wallpaper** |
| Evidence | aurabots ran 16 auras before the 3.16 rework | needed a hard slot cap anyway | WoW **deleted** paladin auras in 5.0.4: *"not much gameplay there"* |

Four findings worth carrying into the design:

1. **A cost is only real if the pool has another consumer.** PoE reservation works because unreserved
   mana is what casts your actual skills. **Reservation with no competing consumer is a slot limit
   wearing a costume.** Our `qi`/`stamina` pools are spent by actions through `CostLedger`, so a real
   contest exists — but only if the aura draws on a pool that side actually spends.
2. **Drain against *regeneration* beats drain against the pool.** GW1 upkeep costs `0.33 energy/sec` of
   degeneration against a `+2`-to-`+4` pip regen; the punishment is "less budget for reactive plays"
   (strategic) rather than "remember to re-toggle" (a chore). GW1 also **auto-drops the most recently
   maintained enchantment at zero energy** rather than soft-locking — a graceful-failure pattern worth
   copying outright.
3. **A subtractive cost-reducer is always a runaway; a divisive one is always safe.** GGG moved
   reservation from `−x%` (additive with itself, singularity at 100%) to `÷(1 + efficiency/100)`
   (asymptote at zero), stating the divisive form *"does not allow for infinite Auras."* **If aura
   upkeep reduction is ever added, it must be divisive from day one.**
4. **Aura *effect* multipliers are far more dangerous than aura *count*.** They multiply across the
   whole set, so their value is superlinear in the number of auras running. PoE had to strip Aura
   Effect from cluster jewels entirely. **A global "aura power" stat is the balance problem, not the
   slot cap.**

Most games converge on **slot cap + resource cost together** (D4: 3 aura slots *and* Fanaticism spends
Faith; GW1: upkeep drain *and* a hard `regen+10` cap). The owner's instinct — a slot *and* a cost — is
the converged-on answer.

Sources: [PoE Reservation](https://pathofexile.fandom.com/wiki/Reservation) ·
[GGG 3.16 manifesto](https://www.pathofexile.com/forum/view-thread/3185299) ·
[GW1 Upkeep](https://wiki.guildwars.com/wiki/Upkeep) ·
[Arreat Summit: Paladin auras](https://classic.battle.net/diablo2exp/skills/paladin-offense.shtml) ·
[Warcraft Wiki: Paladin auras](https://warcraft.wiki.gg/wiki/Paladin_auras)

### 3.3 Anti-stacking techniques, field-proven

- **Highest-only per shared tag** (Warcraft 3): identical auras do not stack; only the strongest allied
  instance applies. Keyed on the *buff object*, so the designer controls stacking per-effect by
  choosing whether to share a tag.
- **One provider per group** (Age of Wonders III): only one hero at a time applies stack-wide bonuses,
  forcing spatial distribution.
- **Bracket separation** (HoMM3, §3.1): needs no special-case rules and is the most robust.

### 3.4 Off-field commanders — the pattern surveyed directly

Surveyed: Advance Wars (all four eras), King's Bounty, Songs of Conquest, Fire Emblem (FE4/FE5/FE10),
Langrisser/Warsong, Ogre Battle, Total War (Warhammer + Rome), Mount &amp; Blade II, EVE Online.
(Disciples turned out **not** to fit — its leader is a full combat unit on the grid; do not cite it.)

**3.4.1 — Almost nobody ships a passive off-field enemy debuff. They use symmetric-differential math
instead.** This is the most directly useful finding in the entire research effort.

Rather than writing an enemy-debuff rule, these games put the commander's contribution on **a stat that
already appears on both sides of one comparison** — and the enemy commander then cancels yours for
free, with no extra mechanic:

- **Fire Emblem** — leadership feeds Accuracy *and* Avoid, and hit resolves as
  `your Accuracy − their Avoid`. 1:1 cancellation on offence and defence simultaneously, zero rules
  written. FE4 `(stars−1)×10`, FE5 `Σstars×3` (army-wide, additive, only nine characters have any),
  FE10 `stars×5` (army-wide, **only the designated commander's stars count**).
- **HoMM3 / King's Bounty** — your hero's Attack is compared against the enemy hero's Defense
  contribution inside one clamped curve. **The enemy commander's Defense literally *is* the debuff on
  your damage.**
- **Songs of Conquest** — same shape, asymmetric conversion (+1%/point up, −0.5%/point down).
- **Advance Wars** — `A_v` and `D_V` both default to 100 and are both CO-modified inside one formula.

Where a real enemy debuff exists it is a **spent resource with a visible meter**, never a standing
aura: Advance Wars CO Powers (Olaf's Blizzard, Hawke's Black Wave for 2 HP to every enemy unit at a
10-star meter cost, Von Bolt's Ex Machina at 10 stars), or HoMM3 hero debuff spells where **mastery
upgrades scope rather than magnitude** (Weakness: Basic one stack → Expert *all* enemy stacks).
A passive enemy-facing aura appears only where the commander is **on the field** (Total War's Vlad,
−4 enemy Leadership).

**3.4.2 — An off-field buff with no counterplay gets patched. Two studios reached this independently.**

*Advance Wars: Days of Ruin* (2008) deleted army-wide CO bonuses entirely and replaced them with a
**CO Zone** — the CO must **board a unit for half that unit's build cost**, projects a radius (0–5)
that grows as a meter fills from damage dealt *inside the zone*, and **dies with its carrier**, emptying
the meter. The developers' stated reason: the change *"was made to put more emphasis on strategy,
rather than on relying solely on a CO's abilities."* The sharpest community articulation names the old
model a *"slippery slope"* and the fix as: *"because the CO is a unit, to counter the CO effects and
any progress the opponent has put into building their CO meter, you simply have to destroy that unit."*

EVE Online reached the same conclusion in the same way — **off-grid boosting was deleted** in Ascension
(Nov 2016); Command Bursts now require the booster inside a 15 km base radius. **Two very different
games, same answer: force the commander into risk.**

⚠️ **This is the sharpest tension in our design and it is named here rather than buried**: our commander
is deliberately off-board and cannot be attacked. We therefore have *no* counterplay mechanism from
either of the two shipped families (kill the carrier; contest the grid). What we do have is the
**upkeep cost** and the **loadout slot** — and per §3.2 those are exactly the "cost + slot" pair the
genre converged on. The honest statement is that our counterplay is economic, not positional.

**3.4.3 — Two channels: one clamped for feel, one open for progression.**

HoMM3, King's Bounty and Songs of Conquest independently converged on the same split: a **clamped
comparison channel** (`f(commanderStat − targetStat)`, max 3×/min 0.33× in both HoMM3 and KB) carries
moment-to-moment feel, and a **separate unclamped, resource-gated channel** (Leadership/Command → army
size) carries actual growth.

**This maps exactly onto `Θ` (contests, difference-based) vs `P(Θ)` (magnitudes) — the ladder we
already have is the right shape.** The commander's contest contribution belongs in the clamped channel
and **must never carry progression.**

King's Bounty is the cautionary tale: its clamped channel **saturated mid-game** (±60 points on stats
that high-tier creatures already exceed), and flat `+3 Attack` items became dead loot. A clamped
channel must stay unsaturated across the whole intended play range — which, for an endless-grind SSOT,
means the clamp has to be on a **ratio**, never on an absolute stat difference.

**3.4.4 — Anti-degeneracy is a roster or scope rule, almost never a diminishing-returns curve.**

Not one surveyed game uses soft-cap decay on commander auras. What they use instead:

| Pattern | Game |
|---|---|
| **Non-stacking within category, stacking across category** — *"the larger of the two bonuses"* | Total War (`Encourage`) |
| **One slot per role; stacking is impossible because there is nowhere for a second bonus to come from** | Mount &amp; Blade II — `EffectiveQuartermaster` returns the holder **or** the leader, never both ⚠️ |
| **One designated commander, full stop** | FE10, Songs of Conquest (one wielder/army), King's Bounty |
| **Separate "has the stat" from "is the commander"** | FE10 ships a 5-star Caineghis whose stars never fire |
| **Additive, but scarce sources and a tiny coefficient** | FE5 (3/star, only nine characters have any) |
| **Roster cap that counts corpses** | Songs of Conquest 0.77.7 (*"dead Wielders count towards the total"*) |
| **`(rank − 1)` offset** — having a commander is free, being a *good* one pays | FE4 |

Total War's rule is worth quoting because it is one line and solves the whole problem: non-stacking
**within** an aura type, stacking **across** types. Its 5.3.0 patch also normalised `Encourage` to a
flat **+4 from every source** (previously +3 characters / +8 units) specifically so players never have
to reason about which source is currently winning.

**3.4.5 — What actually got banned was never the stat percentage.**

Advance Wars' competitive permanent-ban list is led by **economy COs** — Hachi and Colin, because
*"being able to build from cities at half price means that he does not have to play the game by its
rules."* Caulder was banned by Nintendo itself for a permanent **+50 atk/+50 def with 5 HP/turn regen**
that *"functions as a day-to-day CO Power on its own."* Luck COs were banned for *randomness*, not
power.

**A commander who changes what you can afford breaks the game far faster than one who changes a damage
number.** This is a direct warning about Focus (§4.2), whose channels are `resource.efficiency` and
`progression.xpRate` — the economy axis.

### 3.5 Two curve rules worth adopting outright

**Unbounded → linear and uncapped. Bounded → asymptotic and self-saturating.** Derived from Blizzard's
own first-party tables across the entire D2 paladin tree, with no counterexample: Might `+10%/level` to
+230%, Concentration `+15%/level` to +345%, Meditation `+25%/level` to +775% — all linear, all uncapped;
while Fanaticism's *attack speed* track saturates at 35 and Vigor's movement track flattens. **This
satisfies our no-hard-ceilings rule by arithmetic**: linear-unbounded channels never need a cap, and
bounded ones bound themselves without a clamp.

**A scaling stat must have roughly constant marginal value, or you will be forced to cap it.** GGG's
`reduced Reservation` and Riot's percentage CDR failed identically — *"provides very little benefit at
low values, but becomes very powerful when heavily invested in"* — and both were fixed by changing the
arithmetic (`÷(1 + efficiency)`, Ability Haste). **Riot then deleted the 40% cap, because the cap had
only ever existed to contain the exponential.** Fix the arithmetic and the clamp becomes unnecessary —
the same argument our own §11 makes.

**Corollary for cooldowns specifically:** three studios independently converged on
`newCD = baseCD / (1 + haste)`. Never use percentage reduction.

---

## 4. The shape

### 4.1 The core idea

**An aura is a container of `stat.derived` atoms, granted under a `Battlefield × Live × Relation`
scope, held open by an equipped skill that pays a `perTick` cost.**

Every noun in that sentence already exists (§2.1). Because `RelationKind` resolves against the granter
rather than an absolute side, **one authored row serves both factions** — Dave's Might aura and
Zomboss's Might aura are the same content, mirrored.

**How the "both sides" requirement is satisfied — and why it probably needs only one grant.**

The owner's ask is *"buff/debuff all plant/zombie by % for both side."* There are two ways to deliver
it, and §3.4.1 makes a strong case for the simpler one:

| | **A — differential (recommended)** | **B — explicit two-sided grant** |
|---|---|---|
| Grants issued | one: `+m` to `Ally` | two: `+m` to `Ally`, `−m'` to `Enemy` |
| How the enemy is affected | **automatically** — the opposed channel is already the other side of the same contest | a second, separate shift in the same direction |
| Enemy commander cancels yours | **for free**, by the contest math | needs no extra rule either, but now on two axes |
| Prior art | HoMM3, King's Bounty, Fire Emblem, Songs of Conquest, Advance Wars — **all five** | essentially none as a *passive off-field* aura (§3.4.1) |
| Tuning surface | one number | two numbers, and the debuff is asymmetrically useful (§3.1b) |

**Our channels are already opposed pairs read inside one contest** — `combat.power` vs
`combat.defense`, `accuracy` vs `dodge`, `crit.rate` vs `crit.resist`. Buffing ally `combat.power`
*already* shifts the power-vs-defense differential in your favour; additionally debuffing enemy
`combat.defense` shifts the **same** differential a second time. That is double-dipping on one axis,
and it is why every surveyed game writes only the buff half.

**Recommendation: option A.** An aura grants to `Ally` only. "Both sides" is satisfied because both
factions can run auras and they meet in the contest — exactly the HoMM3 property the owner asked for
(*equal commanders cancel; the fight reverts to unit stats*). This also sidesteps the enemy-debuff
problems in §3.1b (asymmetric usefulness against variable-armour zombies, and every encounter needing
to be tuned twice).

✅ **Resolved by the owner 2026-08-30 — option A.** (Recorded because the ambiguity was real:) *"for both side"* may mean "one aura buffs my side and
debuffs theirs" (option B) or "the aura system works for both factions" (option A). The research points
hard at A; the sentence admits both.

```text
aura(aptitude A) = { Ally: +m on A's signature channel }   while enabled, per-tick cost paid
                    …the enemy half emerges from the contest, not from a second grant
```

### 4.2 What each aura grants

The twelve aptitudes already come in opposed pairs — their own catalog descriptions say so
(`Aptitude.cs:28-52`): Onslaught *"breaks guard + reflect"*, Pierce *"breaks mitigation + shield"*,
Precision *"breaks dodge"*, Ferocity *"breaks crit-denial"*.

Under §4.1 option A an aura grants **only** the ally column. The "opposed" column is shown because it
is what the aura is implicitly contesting — it is the *other side of the same differential*, not a
second grant. It is also the check that the set is coherent.

| Aura | Grants to `Ally` | Contests (not granted) | Reads as | Status |
|---|---|---|---|---|
| **Might** | `combat.power` | `combat.defense` | your side hits harder | W3 |
| **Fortitude** | `combat.defense` | `combat.power` | your side takes less | W3 |
| **Vigor** | `combat.shield.capacity` | `combat.shield.pen` | your side is shielded | **live** (ShieldGate is toggle-independent) |
| **Onslaught** | `combat.block.break`, `combat.parry.break` | `combat.block.rate`, `combat.parry.rate` | their guard stops mattering | W3 |
| **Agility** | `combat.dodge` | `combat.accuracy` | your side is hard to hit | W3 |
| **Composure** | `combat.crit.resist`, `.resist.damage` | `combat.crit.rate` | their crits stop landing | W3 |
| **Pierce** | `combat.shield.pen` | `combat.shield.capacity` | their shields stop mattering | **live** |
| **Focus** | `skill.cooldown.*` (tempo only — see §6 Q5) | — | your side acts more often | **R1 — not on the lawn** |
| **Bulwark** | `combat.block.rate`, `combat.parry.rate` | `combat.block.break`, `combat.parry.break` | your side blocks | W3 |
| **Retribution** | `combat.reflect.damage` | `combat.reflect.resist.damage` | attacking you hurts | W4 |
| **Precision** | `combat.accuracy` | `combat.dodge` | your side never misses | W3 |
| **Ferocity** | `combat.crit.rate`, `combat.crit.damage` | `combat.crit.resist` | your side crits | W3 |

**The table is self-checking:** every aura's contested column is another aura's granted column, so the
set is closed under opposition — which is exactly the property that makes option A work. Two commanders
running Might and Fortitude meet in one contest and cancel, with no cancellation rule written anywhere
(§3.4.1). Ten pair cleanly; Vigor↔Pierce is a mutual pair; **Focus is the only aptitude with no opposed
channel**, which is a second, independent signal that it is a different kind of thing (§6 Q5).

**An aura names 1–3 signature channels — never its aptitude's whole edge list.** Three reasons, all
measured from `aptitudes.v2.json`:

1. The `kMilli` weights span **2200×** (Vigor `shield.capacity` 55000 vs Fortitude `reduction` 25). A
   uniform "% of all my aptitude's channels" rule amplifies outliers hardest.
2. Distinctive-channel counts range **3 (Retribution) to 12 (Bulwark)** — the same rule would make one
   aura feel empty and another overloaded.
3. **34 channels are shared by all twelve aptitudes** (5 `resource.max.*`, 5 `resource.regen.*`,
   24 `status.*`). Including the tail would make all twelve auras ~70% identical. **The universal tail
   is deliberately out of scope** — which also makes W5 irrelevant to this design.

### 4.3 Exclusivity and the active set — **owner-decided 2026-08-30**

Guard, the only shipped continuous action, **refuses every other action while it holds**
(`action-ideal.md:42`, decision 3). That is right for a defensive stance and wrong for an aura — a
commander who can do nothing else while their aura runs is not a commander.

So an aura is a **concurrent** continuous action, which is genuinely a new shape here. What it must
inherit from Guard is the slot discipline: *"at `W = 1` an indefinite hold freezes the entire board…
**Guard consumes a slot to RAISE, then releases it. The status persists, not the slot**"*
(`spec-defence-actions.md:85-93`). An aura must hold **loadout capacity**, never the kernel's
concurrency width `W` — and `spec-action-model.md:53-55` warns explicitly that "slot" means both things
in this repo.

**The active-set rule, as decided:**

| | Rule |
|---|---|
| Equipped | An aura occupies **1 of the 5 loadout slots** (`LoadoutSet.MaxSize`), like any other skill |
| Enabling | Costs resource, and pays `perTick` upkeep for as long as it stays on |
| Active at once | **1 by default** — `maxActiveAuras`, a **tunable** so it can be raised to 2+ |
| On exceeding the limit | **The oldest active aura switches off**, FIFO |

Three consequences worth stating:

1. **Equipped ≠ active.** A commander may carry up to five auras in the loadout and have only one
   running. This is a second, independent scarcity on top of the slot — and it is what lets aura
   magnitudes be *large*, since D2's paladin auras could reach +373% precisely because only one ran at
   a time (§3.2).
2. **`maxActiveAuras` being tunable is the right call and matches the evidence.** Four of seven
   surveyed games make stacking structurally impossible; the rest need a stacking rule. Shipping at 1
   means we need no stacking arithmetic on day one, and raising it later is a config change rather
   than a redesign. When it does rise above 1, §3.4.4's rule applies: **non-stacking within an aura id,
   additive across ids.**
3. **FIFO eviction is a deliberate divergence from the one shipped precedent.** Guild Wars 1
   auto-drops the **most recently** maintained enchantment when energy runs out; the owner chose to
   drop the **oldest** instead. GW1's rule protects the player's established setup from an
   over-commit; ours preserves the player's latest intent, which reads better for a deliberate toggle
   than for an accidental over-reservation. **Never silently fail to enable** — evicting the oldest and
   saying so is the honest behaviour, and it must be visible in the UI, not silent.

⚠️ **Eviction must be an explicit, typed outcome, not a side effect.** The action layer already
refuses with typed reasons (`CannotAfford(resourceId)`, `OnCooldown`, `NotBound`) and GG-55 requires
never disabling without saying why. "Enabling Might switched off Fortitude" is the same class of
information and belongs in the same channel.

**Action kinds close at three** (`action-ideal.md:63`, decision 25, owner, 2026-08-27), so an aura is a
`Skill`. There is no fourth kind and this design does not ask for one.

### 4.4 Cost

Legal action costs are **`stamina` (physical), `qi` (skills), `poise` (guard)** — `hp`, `hunger` and
`spirit` are never action costs (`concrete-action-roster.md:407`; note it is **six** resources, not
five — `poise` shipped 2026-08-26, and `resource-hub-ssot.md` still says "five" in two places, stale).
An aura is a skill, so its upkeep is **`qi`**, with `stamina` the alternative if auras should compete
with physical actions instead.

A cost-free aura trips a **blocking** invariant — *"no later layer can repair a pool that refills
faster than it drains"* — which is exactly why Guard has a per-tick hold cost at all
(`spec-action-costs.md` §4.1). **The owner's "nothing free" is required, not flavour.**

⚠️ **`anchorCost(Θ)` has no row in `ssot-power-scale.md` §10's closed inventory**, and `CostLedger`
deliberately leaves the seam inert at 1000‰ rather than invent one — *"inventing one here would be
exactly the private `f(level)` AGENTS.md bans"* (`Cost/CostLedger.cs:32-40`). An aura whose upkeep
grows with level walks straight into that open decision. **Flat upkeep avoids it entirely**, which is
one reason to prefer flat (§5).

### 4.4a The buff/debuff bucket — what to extend (owner ask Q7a, investigated)

**The owner's instinct is right and the seam is already reserved.** But the answer splits in two,
because this repo has *two* modifier pipelines with very different properties.

**The primary pipeline already is the bucket the owner is describing.** `ModifierBag`
(`Stats/ModifierBag.cs:15-49`) is a dictionary keyed by
**`(sourceKind, sourceId, channel, op, applyOwnerKey)`** (`StatModifier.cs:19-20`). It accumulates
per-source, withdraws per-source (`Withdraw`, `WithdrawPlugin`), and composes through
`PhasedComposeStrategy` as **Flat → Increased(sum) → More(product) → Override(priority)**
(`StatComposer.cs:8-35`). Provenance survives all the way to `EntityFinal.Contributions`. This is
exactly the "ten gear pieces stay ten keys" discipline `effect-funnel.md:100` demands.

**And the buff seam is already declared, inert, waiting** — `Stats/Plugins/StubStatPlugins.cs:56-63`:

```csharp
public sealed class BuffStatPlugin : IStatModifierPlugin, IDeclaredInertContributor
{
    public const string Id = "rpg.buff";
    public int Order => 400;
    public void Contribute(StatContext ctx, IModifierBagEditor bag) { }
    public string InertReason => "Buff system unbuilt; seam reserved, not yet a P0 for any program.";
}
```

**⚠️ But aura channels do not live in that pipeline.** `combat.power` is a *derived* channel, and the
derived side has no bucket at all:

| | Primary (`ModifierBag`) | Derived (`ActorDerivedSnapshot`) |
|---|---|---|
| Structure | `Dictionary<key, StatModifier>` | `Dictionary<string, double>` — **one number per channel** |
| Provenance | full — kind, source, plugin, op, priority | **destroyed at `DerivedComposer.Compose`** |
| Accumulates? | yes, per source | **no — recomputed from scratch every resolve** (`ActorHub.cs:51`, *"fresh per call in v1"*) |
| Withdraw one source | yes | **not expressible** |

So **the extension the owner is asking for is: give the derived side the bucket the primary side
already has.** That is the honest shape of the work — not a new subsystem, but parity.

**Three hard blockers this uncovered, all of which must be in the spec:**

- **W7 — a percentage buff on `combat.*` composes to nothing today.** Every `combat.*` channel is
  registered `DerivedComposeKind.FlatSum` (`DerivedStatRegistry.cs:249`), and `FlatSum` sums **only
  `Flat` ops** (`DerivedComposer.cs:42`). An `Increased` modifier on `combat.power.omni` is **silently
  dropped**. A percentage aura therefore needs either a compose-kind change or a Flat formulation.
- **W8 — `stat.derived` does not run on the lawn.** Its runtime matrix is
  `RuntimeSupportMatrix(None, Full, None)` (`AtomKindRegistry.cs:145`) — **lawn None, battle Full, sim
  None** — and its one consumer (`TraitAtomSource.cs:55-90`) never reads `op` at all. Meanwhile
  `stat.modify` is **rejected at bind** for any non-primary channel (`AtomKindRegistry.cs:64-72`). So
  neither atom road delivers a `combat.*` aura to a lawn entity today.
- **W9 — `ActorDerivedSnapshot.Overlay` is replace, not add** (`ActorDerivedSnapshot.cs:47-53`,
  `next._channels[k] = v`). `PatronAuraOverlay.cs:37` compensates by hand
  (`derived.Get(channel) + milli/10.0`). **A second overlay written naively silently erases the
  first.** No arbitration, no guard test. This is the sharpest implementation hazard in the document.

**Why `patron.aura` is not the model to copy.** Its grant is a **lifecycle marker only** — the grant
carries no overlay, and the magnitude lives in process-global static state
(`PatronRuntimeState.MatchAura`) applied by a bespoke injector overlay. It reaches the snapshot with
**no provenance whatsoever**, which is precisely the "unattributed producer" that
`spec-derived-stat-sheet.md:193-199` names (patron, stars, injuries, contracts). Copying that shape
would add a fifth unattributed producer and put us further from GG-49 — *"'Why did my attack drop?' is
answerable from the interface… **Forbids:** a stat readout with no path to its sources."*

**The bucket is what makes GG-49 satisfiable.** Today it holds only vacuously, because no derived value
is shown at all.

### 4.5 The two halves are separate features

Worth stating plainly, because conflating them is what went wrong first time:

| | **Commander stat bleed** (HoMM3 half) | **Aura** (this document) |
|---|---|---|
| Path | `progression.bonus.*` → `ActorHub` → `EntityStatWriter` → vanilla plant/zombie stats | `combat.*` → RPG damage/shield resolution |
| Scope | Every unit, always, no toggle | Side-wide, while enabled, costs upkeep |
| Reach | maxHp, atk, arm (arm is zombie-only; `defense` is W6) | Any of the 84 channels with a live reader |
| Blocked by | **W1 + W2** | W3/W4 per aura |

The bleed is deliberately narrow and that is fine — HoMM3's hero also only contributed
Attack/Defense/HP-shaped numbers. **Fixing W1+W2 delivers the HoMM3 fantasy on its own, with no aura
content at all**, which makes it the natural first slice.

---

## 5. Tunables

**Everything below lives in `data/tuning/aura.v1.json`.** No `const` on this surface. Integer
per-mille throughout (`float` is banned for magnitudes; the ladder is quadratic and `float` stops being
integer-exact at `Θ`=232, inside normal play). Follows the established file convention:
`schemaVersion` · `version` · `_meta{owner,status,note,coverage,measurable}` · typed blocks.

| Block | What it holds | Why it is tunable |
|---|---|---|
| `auras.<id>.signature[]` | the 1–3 channels this aura grants to `Ally` | which channel carries an aura's identity is a balance decision |
| `auras.<id>.budgetMilli` | **the aura's total budget**, split across its signature channels | see the budget model below |
| `auras.<id>.split[]` | how that budget divides across those channels | archetype identity without unequal totals |
| `auras.<id>.upkeep{resource, perTickMilli}` | cost — **flat**, per §6 Q3 | the whole "nothing free" lever |
| `shareScaling{shareExponentMilli, appliesTo}` | how the commander's aptitude share scales the aura | §6 Q1 |
| `stackRule` | `highestOnly` within an aura id, additive across ids | §3.4.4; Total War's one-line rule |
| ~~`appliesToBase`~~ | **removed 2026-08-30.** The aura is not a percentage of anything — it is `k·share^γ·P(Θ)` through the shared read function. The real rule is functional-dependency (never read the channel's current value), which is not a tunable |

**The budget model — the cleanest answer found to the identity-vs-parity problem.** FFXIV's Feint and
Addle are a mirrored pair of *internally asymmetric* debuffs: Feint is **10% physical / 5% magic**,
Addle is **10% magic / 5% physical**, at identical duration and cooldown, both reworked in the same
patch. Square split **one fixed budget (15 points) asymmetrically across two channels, then handed the
mirrored budget to the other role.**

Applied here: every aura gets the **same `budgetMilli`**, divided differently across its own signature
channels. That yields archetype identity, guaranteed parity, and no dead pick — **without any aura's
total needing to be larger than another's.** It also fixes the imbalance §4.2 identified from the
tuning data (Retribution has 3 distinctive channels, Bulwark has 12): under a per-channel magnitude
rule Retribution feels empty; under a fixed-budget rule its three channels simply each get a larger
share of the same total.

**Four numbers that must NOT be authored here**, because other systems already own them:

- The **share → effect** curve. `aptitudes.v2.json`'s `read.*` block already implements PS-3 as two
  tunable functions. An aura reads that; it does not restate it.
- **`P(Θ)` / `Θ`.** Owned by `power-scale.v2.json`. Reading is free; a private curve is the defect.
- **Shield drain priority.** Already locked at `aura 30 → skill 20 → innate 10` (`decisions.md:41`).
- **A debuff ratio.** Deleted from this design — per §4.1 option A there is no separate debuff half to
  ratio, and shipping the knob would invite re-introducing the double-dip.

---

## 6. Open questions

Q1, Q2, Q3 and Q5 now have **evidence-backed answers** from the research in §3 — stated as
recommendations with their reasoning, not as questions. Q4, Q6 and Q7 remain genuine owner calls.

### Answered by research

**Q1 — Does aura magnitude scale with the commander's aptitude share? → YES. Both axes multiply.**

```
Leg B (aura) = k(rung) · share^γ · P(Θ)
```

via the **shared** `AptitudeReadFunctions.Magnitude` — the same read function every other aptitude
consumer calls. The **rung supplies `k`**, the **aptitude share supplies `share^γ`**. Full detail in
[aura-skill/spec-aura-magnitude.md](aura-skill/spec-aura-magnitude.md) §3.

> ⚠️ **This answer was rewritten twice, and both retractions matter — a reader arriving through
> DESIGN-GATE should see them rather than the dead ends.**
>
> **Retraction 1 — "scale on aptitude share" was briefly reversed to "NO", on a wrong argument.** The
> claim was that commander points already write `combat.power.omni` on every fielded actor, so an aura
> scaling on the same share applies one investment twice, *"making the result quadratic in commander
> points."* **The quadratic claim is false.** `share = Total(id)/GrandTotal()` is a **bounded [0,1]
> ratio**; points are unbounded but share is not, and `share² ≤ share`, so the cross-term is *smaller*
> than the linear term beside it. Two contributions summing their `k` into one channel is simply two
> contributions — which is what the bucket exists for.
>
> **Retraction 2 — the replacement, a ladder-independent flat value, was wrong in the opposite
> direction.** With no `P(Θ)` term the aura's share of the total shrinks forever as Θ grows: **a
> progression ceiling by arithmetic**, which the endless-grind SSOT forbids. It is the same defect this
> document flags for `patron.aura` and failed to notice in itself.
>
> **What is actually forbidden**, and the reason is neither of the above: the aura's value must be a
> function of `(k, share, Θ)` **only — never of the channel's current value**. A percentage of the
> actor's existing derived total is **non-idempotent under re-assertion**, and per-tick re-assertion of
> it is **geometric in tick count**. That is the real overflow path (audit D2), and shields have an
> idempotence guarantee for exactly this reason while derived channels do not.

Two constraints on *how*:

1. **Never multiply the channel's current value.** The naive "apply to base, not total" phrasing is
   **not sufficient** on its own, because an actor's base derived value already contains the
   commander-scope contribution. The precise rule is the functional-dependency one above, and it is
   tested by base-independence rather than by a growth ratio.
2. **Marginal value must be roughly constant** (§3.5). GGG and Riot independently shipped the same bug
   and the same fix. Any "aura effect" stat must be divisive/efficiency-shaped from day one, never a
   subtractive reducer.

Curve shape follows D2's rule: **unbounded channels linear and uncapped; bounded ones asymptotic** —
which satisfies our no-hard-ceilings rule by arithmetic rather than by clamping.

**Q2 — Buff/debuff symmetry → the question dissolves; there is no separate debuff half.**

Per §4.1, the recommended shape grants to `Ally` only, and the enemy side is affected through the
contest differential — the design all five surveyed off-field-commander games use. So there is no ratio
to author. `debuffRatioMilli` is **deleted from §5.**

Had we kept an explicit two-sided grant, the answer would have been **1:1**, not the asymmetry I first
suggested: Valve's twelve-year experiment shows symmetric values are correct when *both halves fire
together* (Assault Cuirass +5/−5, never changed in the item's entire history) and wrong only when the
player *chooses* a half (Medallion corrected to +7/−4; Solar Crest's debuff mode deleted outright).

One rule to carry into the spec regardless: **`1 − x` and `1/(1 + x)` are not inverses.** "+33% attack"
against "−33% defence" ships a 1.41:1 defensive thumb that is invisible in the tooltip. Use reciprocal
pairs where cancellation is intended.

**Q3 — Flat upkeep, or scaling? → FLAT.**

Four of six shipped models use flat upkeep with a scaling benefit; PoE's major auras reserve 50% of
mana at gem level 1 *and* at level 20 while the benefit climbs, which is why levelling an aura there
always feels good. The two models that scale cost keep the efficiency curve favourable anyway (D2's
Prayer: benefit ×12.5, cost ×4.5 — investment makes it 2.8× *more* efficient). Clarity is the
cautionary case: cost scales with output, and the documented player response is to level it to 4 and
stop.

Flat also sidesteps the open `anchorCost(Θ)` decision entirely (§4.4).

**Q5 — Focus → make it TEMPO, not INCOME. The distinction is the whole answer.**

The economy-commander research produced a sharp, consistent split across Total War, AoE2 and Rise of
Kingdoms:

- **Tempo bonuses** (movement range, replenishment, cooldowns) are top-tier picks. They convert into
  *more actions per unit of time* — immediate, legible, per-turn.
- **Income bonuses** (recruitment cost, gold) are the trap. A Creative Assembly forum post computes a
  recruitment-cost skill at ~3,600–5,400 gold per campaign and concludes *"every time I see it, I
  respec to remove it."* Income pays out in a currency the player already has too much of by mid-game.

**Focus's channels sit on both sides of that line.** `skill.cooldown.*` is tempo — keep it.
`progression.xpRate` and `resource.efficiency` are income — and `xpRate` is *literally* Magic Find,
which died in Diablo 3 because killing faster produced more loot per hour than Magic Find did.

Two further warnings that apply specifically to Focus:

- **The shared-currency trap.** Rise of Kingdoms' gathering commanders are universally *used* and
  universally *not invested in*, because their sculptures are fungible with PvP commanders. *"If your
  economic commander costs the same resource as your combat commander, it will be judged as a combat
  commander and lose."* **Focus's aura would be bought with the same aptitude points as Might's.**
- **Unverifiable is worthless.** Bannerlord's community cannot distinguish "this governor perk is weak"
  from "this governor perk is broken," and has settled on not bothering. Whatever Focus does must be
  visible in the battle report — and per §3, counterfactual metrics (casts enabled, resource that would
  have run out), never a throughput column it will always lose.

*Recommendation: Focus's aura is cooldown-only, using `newCD = baseCD / (1 + haste)` (three studios
converged on this form, and Riot deleted its 40% cap once value became linear). It ships when the
action layer runs in the relevant host — R1 is real and no amount of design removes it.*

### Owner-decided 2026-08-30

**Q7 — *"for both side"* → the aura system works for both factions; an aura grants to its own side
only.** §4.1 option A. Both commanders run auras and meet in the contest; equal commanders cancel, as
in HoMM3. No separate debuff grant, no debuff ratio to tune.

**Q7a — NEW ARCHITECTURAL ASK, arising from that decision.** Owner: *"we need build buff/debuff bucket,
each scope should have a bucket to accumulate buff/debuff, like actor scope (multiple buff/debuff on an
actor) should have its own bucket — we already have actor stats bucket right, so I think this time to
extend it."*

So the design gains a **per-scope accumulation bucket**: each scope (actor, side, match, …) owns a
bucket into which buffs and debuffs accumulate, extending the existing actor stats bucket rather than
introducing a parallel structure. **This is scoped as an extension, not a new subsystem** — the
existing provenance discipline must survive it (`effect-funnel.md:100`: *"Funnel must not compose
Flat→Inc→More across distinct modifier sources… Folding them into one Grant overlay means unequip
cannot withdraw one Xi"*). A bucket that loses per-source provenance would break withdrawal, which is
the exact defect that warning exists to prevent.

*Investigation of the existing accumulation machinery is in flight; this section will name the specific
class to extend once it reports.*

**Q8 — Active-aura set → 1 at a time by default, `maxActiveAuras` tunable, oldest evicted on
overflow.** Fully specified in §4.3. Equipped (5 loadout slots) and active (1, tunable) are two
independent scarcities.

### Still genuinely open — owner calls

**Q4 — Is `OVERLAY-COMBAT` (W3) meant to ship off? → ANSWERED: nobody switched it on. It is unfinished
wiring, not a guard — but flipping it is not free.**

The toggle (`OverlayCombatFeature.cs`, 14 lines, no justifying comment anywhere) was **born default-off
in a commit whose subject is *proving* the feature**. `docs/research/effect-runtime/_prove-overlay-combat.json`
is committed, timestamped four minutes later, and records **10/10 PASS (C1–C10) on a real lawn**. Yet
`debug-live-checklist.md:277-286`'s Pass column is still blank and `04-proof-results.md:131` still says
PENDING. No `decisions.md` row, code comment, or spec sentence says it should stay off, and
`decisions.md:40` mandates *"one combat formula set + one apply path, everywhere"*, which points the
other way. Sibling `SYS-*` flags **were** promoted to default-true (`CheatSchema.cs:99-101`), so the
move exists and was simply never made here.

**No goldens move** — proven, not assumed: the flag lives in `FusionRpg.Injector`;
`Core.Tests.csproj:24` references only `FusionRpg.Core`; battle and sim construct
`OverlayCombatCalculator` unconditionally (`BattleRunState.cs:108`).

⚠️ **Two real live-lawn behaviour changes, and the second is a genuine gap:**

1. **A hit can now deal 0.** The overlay profile has **no chip floor** — `CombatProfiles.cs:12`,
   `Overlay = new(0)`, versus 50‰ for battle/sim. A fully-mitigated overlay hit resolves to zero where
   today it always lands for its authored amount.
2. **Heals change, and the 2026-08-20 proof does not cover them.** `Finalize` checks `signedAmount > 0`
   *before* the payload check and routes to `FinalizeHeal`, which adds `combat.heal.power`.
   `git log -S FinalizeHeal` dates that to **2026-08-25 — five days after the proof ran.** The proof's
   own C5 result (*"heal pass-through"*) no longer describes enabled behaviour. **Every overlay heal on
   the lawn would begin scaling with the healer's `heal.power`, on a path never live-tested.**

*Recommendation: flip it, but re-prove the heal path first. That is a real gap, not paperwork.*

**Q6 — Which container carries aura content? → ANSWERED: it was a false choice. Both.**

Container and grant are **orthogonal layers**, confirmed in code. A container (`ContainerRow.cs:37-39`)
is *"mechanism, not content… what a skill contains — never when it fires"*; a grant is a live binding
of a compiled def to an owner scope. `AtomCompiler.EmitDefAndGrant` is the bridge, emitting one
`EffectDefDto` **and** one `EffectGrantDto`. **Nothing in `ContainerKind` gates owner scope.**

So: **`world-buff.*` as the container** (making the reserved-but-never-authored plumbing real —
`ContainerKind` is a closed six-member enum, `ContainerRow.cs:3-15`), delivered by an **ordinary
battlefield scope grant** supporting `Grant`/`WithdrawForOwner` at arbitrary times — which §4.3's
toggle-and-evict model requires and which `patron.aura`'s board.start/board.end lifetime does not
provide.

**Q9 — Does a commander aura conflict with another feature? → ANSWERED: yes, one genuine conflict and
two lesser ones.**

| Feature | Verdict |
|---|---|
| **Commander-scope aptitude allocation** | ⚠️ **GENUINE DOUBLE-COUNT.** Commander points already write `combat.power.omni` on every fielded actor (`aptitudes.v2.json:131`; `spec-point-economy.md` §2.1 *"applies to every demon you field"*; `decisions.md:101`). An aura scaling on that same investment and granting that same channel applies it twice — quadratically, since the base already contains the commander term. **Latent on the lawn only because W1 leaves the allocation `Empty`; live in sim today.** Fixing W1 and shipping the aura in one slice is what turns it from latent to shipped. Drove the Q1 correction. |
| `patron.aura` | **Overlaps, does not duplicate** — element channels vs omni, different source, additive by the shipped `omni + element` rule. ⚠️ But the **W9 clobber hazard** applies. Also: patron clamps at **150‰** (`patron.v1.json:10`) while an aura would be unclamped, so patron becomes irrelevant past ~15 points. |
| `commanderOnly` item role | **Unacknowledged second answer to the same question.** Already one slot, match owner-scope, whole-squad reach, its own separate 100‰ budget (`core.v1.json:324-331`), and — like `world-buff.*` — **never authored**. Whether banner atoms and aura atoms stack, and against which budget, is undecided anywhere. |
| Shield aura priority (`decisions.md:41`) | **No conflict** — a different meaning of "aura" (shield-instance drain order). Minor: a shield-*granting* aura would share sourceId space and the cap-3 admission rule with `ShieldAuraGrants`. |
| `WorldState.ScopeModifierMilli` | **Not the same job** — `WorldMap` scope, not `Battlefield`. No conflict and no claim on it. |

**Q10 — NEW, forced by the Q1 correction: what is the aura's own investment axis?** Aura level / skill
points in that aura (the D2 shape)? A separate currency? Or flat-authored per aura with the aptitude
supplying identity only? **Owner call — this is the last thing blocking a spec.**

### One tension this design does not resolve, stated plainly

Advance Wars and EVE Online independently concluded that **an off-field commander buff with no
counterplay has to be given a physical address** — Days of Ruin made the CO board a killable unit; EVE
deleted off-grid boosting outright. Our commander is deliberately off-board and cannot be attacked, so
neither shipped counterplay family is available to us.

**Our counterplay is economic (upkeep + a loadout slot), not positional.** That is the "cost + slot"
pair the genre converged on (§3.2), so it is defensible — but it is a real difference from every game
surveyed, and it should be named in the spec rather than discovered later.

---

## 7. What this document deliberately does not decide

- **Who Crazy Dave and Dr. Zomboss are** as playable/AI identities, or any commander roster (R3). They
  are world-map faction ids today and nothing more.
- **The "commander joins battle directly"** combat-participant case for expeditions/world-map/web-RPG —
  explicitly deferred alongside this discussion (`buff-debuff-scope-ideal.md:226-229`).
- **Any coefficient.** Every number in §5 is a named slot, not a value. Coefficients come after a
  measurement pass, and `aptitudes.v2.json`'s own `_meta.measurable` is the model for admitting which
  ones are designed rather than measured.
- **Build sequencing.** That is `/plan`'s job. The observation that W1+W2 is the natural first slice
  (§4.5) is an observation, not a plan.

---

## 8. Design-gate checklist

```
[x] I identified the subsystems: action layer, effects/atom layer, scope primitive, stats/derived,
    power ladder, resources, status, match lifecycle, server-vs-injector, UI.
[x] I read every §1 row for those subsystems this session, plus docs/design/spec-action-layer.md.
[x] I checked decisions.md for locks covering this — rows 1/3/25 (action kinds, guard-as-stance),
    41 (shield/aura priority), 101 (four-scope allocation), 103 (buff-debuff scope + this deferral).
[x] Every factual claim cites file:line or a document section.
[x] I verified claims against CODE, not comments — LoadoutSet.MaxSize, ActionCostTiming.PerTick,
    WhoKind/WhereScope/RelationKind, AptitudeCatalog, and the 486-edge/84-channel counts were each
    read or counted directly. The stale "zero hits in src/" claim in spec-action-layer.md was caught
    this way.
[x] I read the surrounding section of every rule I quoted.
[~] I tested (not assumed) constraints I report. PARTIAL: the wiring gaps W1-W6 are read from source,
    not observed at runtime — no test suite was run and the game was not launched for this document.
    W3's runtime state in particular depends on the owner's persisted cheat document, which is not
    inspectable from source. Stated, not hidden.
[x] Nothing contradicts a §2 invariant. The one new shape (concurrent continuous action, §4.3) is
    named explicitly as new rather than presented as existing precedent.
[x] Corrections propagated: the RPG-layer rule is now in CLAUDE.md; the first pass's wrong "5 of 12"
    conclusion is recorded in §0 rather than quietly dropped.
```

**Known-stale documents found while writing this** (flagged, not fixed): `DESIGN-GATE.md:40` says
7 atom triggers, code has 8 · `resource-hub-ssot.md:126,238` say "five resources", it is six ·
`concrete-action-roster.md` §10's runtime matrix predates A18c-A18e · `docs/design/spec-action-layer.md`
says `rpg_action` has "zero hits in src/", which was true on 2026-08-23 and is not now.
