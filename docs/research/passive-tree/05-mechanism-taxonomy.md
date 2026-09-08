# Mechanism taxonomy — what a deep-tier passive can grant that a bigger number cannot

**Status:** research, 2026-09-05. Not a spec, no build authorized. Written against
[passive-tree-ideal.md](../../architecture/passive-tree-ideal.md) §3.5, which measured tree power as
aptitude-point-equivalents across `b ∈ {0,2,5,10,20}` × `Fmax ∈ {1.0,1.25,1.5}` and found that **not one
cell reverses the ordering** — spreading beats concentrating at every setting. Its conclusion is the
charter for this document:

> **A focus build cannot be rescued with MAGNITUDE. It can only be rescued with MECHANISM.**

Every claim below is tagged **FACT** (verified against code this session, `file:line` cited),
**INFERENCE** (derived from cited code, not measured), or **RECALL** (from a document, not re-verified).
Code beats docs; docs beat comments.

---

## 0. Answer up front

### The ranked top five

Ranked by (value to a **focused** build) × (buildable today). The acceptance test throughout is
§3.5's: *a mechanism that helps every build equally does not solve the problem.*

| # | Mechanism | Value to a focus build | Buildable today | Verdict |
|---|---|---|---|---|
| **1** | **Erosion — a flat, per-layer defensive debuff** (the anti-turtle punish) | **Highest.** Its value is proportional to the *opponent's* breadth and is near zero against another corner — the only shape in this document that raises corner-vs-spread without also raising corner-vs-corner | **Wiring gap**, one named line | Design it. §4c |
| **2** | **Retaliation / reflect** | High for a *defensive* corner — converts survival into offence, the exact hole a Bulwark/Fortitude corner has. Worth nothing to a Might corner | **Built and live**, including the production caller | Ship content, not code |
| **3** | **Threshold trigger** ("below 30% HP, gain X") | High. A discrete, layer-independent grant a corner can afford once, with no second layer required | **Fully expressible today** — leaf, trigger and three writer kinds all present | Ship content, not code |
| **4** | **Conditional scaling** ("damage scales with damage taken") | Highest headline value — this is `class-system-map` §4b's own first named fix | **Wiring gap on both hosts.** The Battle recompose seam exists and is deliberately never called mid-fight; the lawn's status→derived path composes nothing | Two small wirings |
| **5** | **Layer parity — a floor on every defensive channel** ("close eleven layers cheaply") | Highest *theoretical* value — it attacks §3.5's stated cause directly | **New code, no new architecture.** A fourth `IActorStatSubsystem`; no vocabulary change | Bounded by the even-split ceiling (§5) |

Below the line, and why: **layer denial / bypass** (§3, M5/M10) is the single most valuable class for a
focused build and is the one item in this document that is a **genuinely new capability** — every
shipped "breaks their X" channel is a saturating contest that provably never reaches zero.
**Resource trades** (M4), **cost-structure changes** (M8) and **element conversion** (M2) are all real
wiring gaps rather than walls, but none of them targets the focus/spread gap specifically.

### The measurement verdict

**The closed form can be extended for the phase-shaped and stat-shaped mechanisms, and must be for the
rest — but a full battle simulation does not need building, because two already exist and neither is
wired to the dominance matrix.**

- `StrikeMixture` calls the shipped resolver functions rather than re-implementing them
  (`src/FusionRpg.Core/Balance/Analytic/StrikeMixture.cs:16-20`), so **any mechanism implemented as a
  change to `DivisiveMitigation` / `PierceFactor` / `AmpFactor` / `CapAvoidanceBand` is scored for free,
  with no harness change at all.** FACT.
- Threshold triggers and ramping buffs are **phases**, and `PhaseModel.ShieldEffectiveHp` already proves
  the pattern (`src/FusionRpg.Core/Balance/Analytic/Predictor.cs:130-135`). Extending the closed form
  covers them. ~1 focused session.
- Per-hit triggers with ICDs, charges, stacking and timing are **outside** the closed form by
  construction — `Predictor` models exactly one swing per side per round
  (`Predictor.cs:161-171`). These need trials.
- The trial engines exist: `tools/CombatSim/Simulator.cs:59` drives the real
  `CombatDamageDispatcher.DispatchInstant` through `FoundationHarness` including funnel, shield gate and
  reflect; `BattleEngine` is a pure, seeded, no-I/O resolver on the same SSOT path
  (`src/FusionRpg.Core/Battle/BattleEngine.cs:10-20`). **Neither is reachable from
  `DominanceGuard.Measure`, which calls `Predictor.Predict` (`DominanceGuard.cs:55`).**
- **The real blocker is not the simulator.** `BattleEngine` fires no `OnDamageTaken` / `OnDamageDealt`
  atom triggers at all — grep over `src/FusionRpg.Core/Battle/` returns zero hits, while the lawn fires
  both (`src/FusionRpg.Injector/Effects/EventDrainHost.cs:95-99`). A trigger-driven mechanism node cannot
  be measured in Battle until Battle fires the triggers.

Full recommendation with effort estimates: **§6**.

---

## 1. The resolver map — every stage, in order, as shipped

The whole damage path, from packet to HP. All FACT.

### 1.0 The structural fact that shapes the entire taxonomy

**`OverlayCombatCalculator.Compute` is a pure function of `(request, rng)`** — it reads two frozen
`ActorDerivedSnapshot`s and the packet, and it has **no hook, no callback and no atom trigger anywhere
inside it** (`src/FusionRpg.Core/Combat/OverlayCombatCalculator.cs:57-314`). The four board-event atom
triggers (`OnSpawn`, `OnDamageDealt`, `OnDamageTaken`, `OnDeath` —
`src/FusionRpg.Core/Effects/Atoms/AtomKind.cs:104`) fire from the injector's event drain, *outside* the
math.

So there are exactly **three attach points** for any mechanism, and every entry in §3's taxonomy lands on
one of them:

| | Attach point | Vehicle | Constraint |
|---|---|---|---|
| **(A)** | **Snapshot-time** — change a derived channel before the hit resolves | `stat.derived` atom (`AtomKindRegistry.cs:505`), or an `IActorStatSubsystem` folded by `ActorHub.ResolveDerived` (`ActorHub.cs:56-59`) | `stat.derived` declares `AtomTriggers.None` (`AtomKindRegistry.cs:535`) — it is a permanent modifier, so conditionality here cannot read per-hit state |
| **(B)** | **Packet-time** — change the packet before `Finalize` | `ElementPayload`, `SignedAmount`, `EffectivenessMultiplier`, `CombatProfile` | No atom kind writes any of these. The grant that builds the packet does |
| **(C)** | **Post-hit** — fire a new packet or grant from a damage event | `resource.delta` / `status.apply` / `shield.grant` / `stat.modify`, all carrying `AllTriggers` (`AtomKindRegistry.cs:46-48`) | A separate Funnel event, never a callback inside the mitigation math |

**This is the single most useful thing to know before designing a passive node.** "React mid-hit" is not
a thing the architecture offers; "look different when the hit resolves" and "do something afterwards"
both are.

### 1.1 Stage table

| # | Stage | `file:line` | Reads | Writes / produces | Trigger available |
|---|---|---|---|---|---|
| S0 | Proc-depth gate | `CombatDamageDispatcher.cs:27-32` | `packet.ChainDepth`, `policy.ResolveProcDepthLimit` | `skipped += ":proc-depth"` | — |
| S1 | Target resolve | `CombatDamageDispatcher.cs:34` | `packet.Target`, `BoardSnapshot`, `ev` | ptr list | — |
| S2 | `math.Finalize` | `CombatDamageDispatcher.cs:40` → `OverlayCombatMath.cs:37` | `packet.ElementPayload` | signed long | — |
| S2.0 | **Payload gate** | `OverlayCombatMath.cs:42-47` | `ElementPayload` count | **A packet with no element payload returns unchanged — the entire resolver is skipped** | — |
| S2.0b | Heal branch | `OverlayCombatMath.cs:39-40`, `:77-88` | `resource.restore.hp` of the *healer* only | floored at 0 | — |
| S2.1 | Effectiveness scale | `OverlayCombatCalculator.cs:68` | `request.EffectivenessMultiplier` (`skill.effectiveness.{category}` via `:37`) | `effectiveBaseDamage` | — |
| S2.2 | Element matchup | `:76-84` → `ElementHub.cs:26`, `:17-21` | component weights, defender `ActorElementTypes` | `matchupBonus`; **multiplies across the defender's two element slots** | — |
| S2.3 | Per-component fold | `:128-173` | `combat.power.*` `:135`; `combat.penetration.*` − `combat.absorption.*` `:141-142`; `combat.defense.*` `:143`; `combat.amplification.*` − `combat.reduction.*` `:159-160`; `combat.accuracy.*` − `combat.dodge.*` `:162-165`; `combat.crit.rate.*` − `combat.crit.resist.*` `:166-169`; `combat.crit.damage.*` − `combat.crit.resist.damage.*` `:170-173` | `weightedDelta`, `weightedOffense`, `weightedDefense`, `weightedPowerOnly`, `ampDelta`, `pHitFinal`, `pCritFinal`, `critMultFinal` | — |
| S2.4 | Attack table | `:183-188` → `CapAvoidanceBand :344` | `combat.parry.rate/break`, `combat.block.rate/break` — **omni only** (`CombatDerivedReader.cs:53-65`) | `pParry`, `pBlock`, cumulative avoidance capped at 950‰ | — |
| S2.5 | **One RNG draw** | `:218-220` → `ResolveBand :326` | one `rng.Next(1_000_000)` | miss / parried / blocked / clean, a partition of [0,1) | — |
| S2.6 | Crit roll | `:222-223` | `pCritFinal` | `crit` | — |
| S2.7 | Mitigation | `:225-232` → `DivisiveMitigation :426` | offense, defense, `k`, `ladderScale = base + weightedPowerOnly` | `powerAdjusted`. Negative defense mirrors around 1.0 (`:439-441`) | — |
| S2.8 | Parry/block branch | `:236-266` → `ClampedContest.cs:40` | `combat.parry.strength/shred`, `combat.block.strength/shred` | **"no block, no mitigation" — the whole mitigation chain is skipped for these two outcomes** | — |
| S2.9 | Crit ×, amp ×, chip floor | `:271-272`, `:282-284`, `:288-294` | `critMultFinal`, `ampDelta`, `Profile.MinChipShareKPm` | `finalDamage`. Amp unclamped upward (PS-8, `:392`, `:402`) | — |
| S2.10 | Round to signed long | `:297` | — | `signedDelta` | — |
| S3 | **Reflection** | `CombatDamageDispatcher.cs:69-71` → `TryReflect :85` | `combat.reflect.rate` − `combat.reflect.resist.rate` `:99`; `combat.reflect.damage` − `combat.reflect.resist.damage` `:103` | a **new** reversed packet, `ChainDepth+1`, **no element payload** `:108-121`, re-entering at S0 `:122` | — |
| S4 | **Shield gate** | `DamageApplyPipeline.cs:115-122` → `ShieldGate.cs:72` → `ShieldRuntime.cs:287-338` | `combat.shield.pen` (attacker), `combat.shield.toughness` (owner) `ShieldRuntime.cs:307-312`; `ShieldElementMatrix` via `ShieldMath.cs:105` | drains the stack in priority order; remainder reaches HP. **Overflow throws** (`ShieldMath.cs:76`, `ShieldInputOverflow`) | — |
| S5 | Funnel enqueue | `DamageApplyPipeline.cs:124-136` | — | `EnqueueMutation(entity:{ptr}, amount, channel "hp")`, then `NoteOverlayDamage` | `OnDamageDealt` / `OnDamageTaken` fire here-ish, from the injector drain (`EventDrainHost.cs:48`, `:99`) |
| S6 | FA10 apply | out of scope — Funnel → Writer | — | HP delta | — |

**Two facts from this table that get designed against wrongly:**

1. **S2.0 — a damage packet with no `ElementPayload` bypasses the entire resolver**
   (`OverlayCombatMath.cs:42-47`). Every mitigation, evasion and crit layer is skipped. The reflect
   bounce is built deliberately payload-less for exactly this reason (`CombatDamageDispatcher.cs:81-83`),
   so a bounce is never re-mitigated. FACT.
2. **S3 branches off the same packet the shield gate receives, not after it** — so under the shipped
   `reflectReadsPostShield: true` (`data/tuning/combat.v1.json`) it reads the post-shield amount, and a
   fully absorbed hit reflects nothing (`CombatDamageDispatcher.cs:65-69`). FACT.

### 1.2 The whole gate is default-off in the injector

`ConditionalOverlayCombatMath.Finalize` delegates to the overlay pipeline only when `IsEnabled()` returns
true (`src/FusionRpg.Core/Combat/ConditionalOverlayCombatMath.cs:23-25`), and the injector wires that to
`OverlayCombatFeature.Enabled` (`src/FusionRpg.Injector/Effects/EffectRuntime.cs:482`), which is

```csharp
public static bool Enabled => EnvEnabled || CheatState.On(CheatToggleId);
```

— `src/FusionRpg.Injector/Effects/OverlayCombatFeature.cs:13`. **Inert line, named.** This is a default-off
toggle, i.e. a **wiring gap**, not an architectural limit. Everything in this document resolves inside
the RPG layer regardless; the toggle only decides whether the lawn *host* runs it today. FACT.

---

## 2. Why breadth wins — from the code

§3.5 states the reason in one sentence: *"defensive layers compose multiplicatively."* Here is the exact
arithmetic, with the lines that multiply.

### 2.1 The product

The measured harness resolves through the omni path (`StrikeMixture.cs:22-27`, which mirrors
`OverlayCombatCalculator`'s `Components.Count == 0` branch). Expected damage per swing is an exact finite
mixture over five atoms (`StrikeMixture.cs:35-38`). Substituting the atom definitions
(`StrikeMixture.cs:89`, `:94-102`, `:119-124`), the clean-hit path is:

```text
E[D_clean] =  pHit                                  ... logistic  (accuracy − dodge)
            × (1 − parryShare − blockShare)         ... linear, capped 950‰
            × (base + power)                        ... the only non-saturating term
            × K / (K + defense · pierceFactor)      ... hyperbolic
            × ampFactor                             ... hyperbolic below zero
            × (1 + pCrit · (critMult − 1))          ... logistic × logistic
```

Every factor is on a different aptitude, and **every factor except `(base + power)` is individually
saturating.** FACT, line by line:

| Factor | Shape | `file:line` | Aptitude(s) |
|---|---|---|---|
| `pHit` | logistic `1/(1+e^-x)` | `OverlayCombatCalculator.cs:163-165` → `CombatProbability.cs:8` → `ResistanceEvaluator.cs:123-124` | Agility ↔ Precision |
| parry/block share | linear, cumulative avoidance capped at 950‰ | `OverlayCombatCalculator.cs:183-188`, `:344-355`; `combat.v1.json avoidanceBandCapPermille: 950` | Bulwark |
| `K/(K+defense)` | hyperbolic, asymptotic to 0 | `OverlayCombatCalculator.cs:426-442` | Fortitude |
| `pierceFactor` | `1/(1+max(0,penΔ)/10)`, bounded **(0,1]** — never reaches 0 | `OverlayCombatCalculator.cs:382-383` | Pierce ↔ Fortitude |
| `ampFactor` | reciprocal branch `1/(1−d/s)` for `d<0` | `OverlayCombatCalculator.cs:402-406`; `combat.v1.json ampShape: "reciprocal"` | Fortitude |
| crit term | logistic rate × logistic magnitude | `OverlayCombatCalculator.cs:166-173` | Composure ↔ Ferocity |

And two more layers sit *outside* the product:

- **Shield** — an additive effective-HP phase ahead of HP, drained layer by layer
  (`DamageApplyPipeline.cs:115-122`, `ShieldRuntime.cs:303-338`). Vigor.
- **Reflect** — a probability × share bounce onto the attacker (`CombatDamageDispatcher.cs:99-105`).
  Retribution.

### 2.2 Why a product of saturating factors rewards spreading

Survival is monotone in `−log E[D] = −Σ log f_i`. Each `log f_i` is **concave** in the points spent on
that layer, because logistic and hyperbolic are both concave over the region an allocation reaches, and
the aptitude read is **linear in share** — `read.contest.shareExponentMilli: 1000` and
`read.magnitude.shareExponentMilli: 1000` in `tools/CombatSim/tuning/aptitudes.v1.json`, i.e. `γ = 1`.
FACT for the shapes and the exponent; **INFERENCE** for the concavity conclusion.

Maximising a sum of concave functions under a linear budget equalises marginal returns, which means the
optimum **spreads**. A corner build does the opposite: it pushes one `f_i` deep into its saturated region,
where the marginal return is near zero, and leaves the other five at their steepest point — which is
exactly where the opponent's corresponding offensive aptitude gets *its* maximum marginal return.

**This is why §3.5's sweep could not move the needle.** At `b = 20` the corner's spike share climbs from
0.54 to 0.71 and the win rate does not move, because the extra investment is spent further along a
saturating factor. More Might does not fill an empty defensive layer — and now we can say precisely what
"empty" means: a factor sitting at its steepest, most exploitable point.

### 2.3 The offensive side saturates for the same reason

`DivisiveMitigation` reads `ladderScale = BaseOverlayDamage + weightedPowerOnly`
(`OverlayCombatCalculator.cs:232`, doc `:416-426`), so `K` grows with power and the *mitigated fraction*
is constant with respect to power. More `combat.power.omni` is therefore **linear**, not saturating —
that is the one term in §2.1 that does not diminish. FACT.

But it is still multiplied by five factors Might cannot touch. And in the shipped harness the base hit is
**zero** — `TerminationGuard.ToActor` constructs every measured actor with `BaseDamage: 0.0,
ShieldMaxHp: 0` (`src/FusionRpg.Core/Balance/Guards/TerminationGuard.cs:123`) — so offense *is* power, and
a Might corner's whole output passes through `pHit × (1−avoid) × mitigation × amp × crit`, five factors
it bought nothing for. **INFERENCE**, but a tight one.

### 2.4 Element typing multiplies too

`ElementHub.ResolveComponentBonus` multiplies the matchup across the defender's primary **and** secondary
element slots (`src/FusionRpg.Core/Combat/Element/ElementHub.cs:17-21`). Breadth in *typing* is rewarded
by the same construction. It is not in the measured number — the harness neutralises elements
(`StrikeMixture.cs:22-24`, and `DominanceGuard.StandardCoverage` reports `ElementAxis: "NEUTRALISED"`,
`DominanceGuard.cs:82`) — so this is a **known-unmeasured** extra reason breadth wins. FACT for the code,
FACT for the coverage note.

---

## 3. The taxonomy

Ten classes. For each: what it does, whether the shipped resolver plus the closed atom vocabulary express
it today and where it attaches, and — the acceptance test — whether it helps a **focused** build
specifically.

### Summary table

| | Class | Expressible today? | Attaches at | Helps a focus build? |
|---|---|---|---|---|
| **M1** | Conditional scaling | **Wiring gap**, two named lines | (A) snapshot, (C) post-hit | **Yes** — this is `class-system-map` §4b's own first named fix |
| **M2** | Conversion / reroute | Resolver yes; **no passive attach point** | (B) packet | Neutral |
| **M3** | Threshold trigger | **Yes, fully** | (C) post-hit | **Yes** — discrete, layer-independent |
| **M4** | Resource trade | **Wiring gap** — 5 of 6 resources inert | (C) post-hit | Neutral to weak |
| **M5** | Denial of an opponent's layer | **Genuinely new capability** | would be (B) or a status read | **Yes, most of all** |
| **M6** | Stacking / decay change | **Yes** for status; partial for shield | (A) snapshot | Only if the build already holds that axis |
| **M7** | Retaliation | **Built and live** | S3, already in the resolver | **Yes**, for a *defensive* corner |
| **M8** | Cost-structure change | **Wiring gap** — resolver read exists, no caller | (B) packet | Neutral |
| **M9** | Timing change | **Yes on the lawn**; invisible to the closed form | (A) snapshot, primary channels | Weak, and unmeasurable today |
| **M10** | Layer piercing / bypass | Reduction yes; **bypass is new** | S2.3 / S2.4 / S4 | **Yes** |

---

### M1 — Conditional scaling

**What:** power that reads a live state variable — HP fraction, missing HP, damage taken this round,
a stack count. *"A passive scaling damage with damage taken."*

**Today: WIRING GAP, and the gap is precisely locatable.** Three parts, two of which are already built:

1. **The condition is built.** `LeafId.HpBelowMilli` / `HpAboveMilli` are in the closed leaf list
   (`src/FusionRpg.Core/Effects/Atoms/PredicateNode.cs:24-25`) **with real readers** —
   `FactReader.HpMilli` (`src/FusionRpg.Core/Effects/Atoms/FactReader.cs:71`), reading a pre-resolved
   `EntityFacts.HpMilli` (`:9`). Not a declared-only leaf. FACT.
2. **The trigger is built.** `OnDamageTaken` (`AtomKind.cs:85`) fires on the lawn
   (`EventDrainHost.cs:95-99`, `GameHooks.cs:666`). FACT.
3. **The writer is the gap.** The only kind that writes a `combat.*` derived channel is `stat.derived`,
   and it declares **`AtomTriggers.None`** — `src/FusionRpg.Core/Effects/Atoms/AtomKindRegistry.cs:535`.
   It is a permanent modifier by construction, so it cannot re-evaluate per hit. The *triggered* stat kind,
   `stat.modify`, carries the full trigger set and `TriggerOptional: true`
   (`AtomKindRegistry.cs:497`, `:503`) but writes only the **23 primary** Unity channels
   (`AtomKindRegistry.cs:480-481` → `StatChannels.All`, listed at
   `src/FusionRpg.Core/Stats/ModifierOp.cs:28-66`). FACT.

**The near-miss route, and why it is also a gap.** A timed status looks like the answer:
`StatusStatPayload` explicitly accepts **derived** channels — its own worked example is
`{"combat.power.fire": {"flat": 25}}` (`src/FusionRpg.Core/Status/StatusStatPayload.cs:30-32`), validated
via `IsCombatChannel` (`:127`), and `status.apply` carries `OnDamageTaken` (`AtomKindRegistry.cs:610`,
`:46-48`). But on the lawn the consumer upserts into the **primary** `StatSystem` session bag
(`src/FusionRpg.Injector/Effects/EffectRuntime.cs:81` → `StatusStatPayload.ToModifiers`, `:148-177`), and
`StatSystem.Resolve` composes into `EntityFinal` — the 23 Unity fields
(`src/FusionRpg.Core/Stats/StatSystem.cs:148-167`, `src/FusionRpg.Core/Stats/EntityBaseline.cs:48-73`).
Meanwhile `ActorHub.ResolveDerived` folds **only registered `IActorStatSubsystem`s**
(`src/FusionRpg.Core/Stats/Derived/ActorHub.cs:56-59`), and the lawn registers exactly three —
`RpgProgressionSubsystem`, `AptitudeSubsystem`, `AtomDerivedSubsystem`
(`ActorHub.cs:145-155`, wired at `src/FusionRpg.Injector/CheatState.cs:47-55`). **None reads the session
bag.** So a status naming `combat.power.omni` is validated, stored, withdrawn on expiry, and never
composed — the exact failure `StatusStatPayload`'s own doc refuses in the abstract
(`StatusStatPayload.cs:79-81`). FACT.

**Battle has the seam and does not call it.** `BattleDerivedModifierLedger` is a live, sourced,
idempotent recompose for `combat.*` channels (`src/FusionRpg.Core/Battle/BattleDerivedModifierLedger.cs:6-27`),
and `BattleRunState.RecomposeDerived` is its entry point — but its own doc says it is *"deliberately not
called anywhere in `Resolve`'s own loop"* (`src/FusionRpg.Core/Battle/BattleRunState.cs:124-127`), and the
only caller is at construction, for auras (`BattleRunState.cs:280-288`). **Inert line, named.** FACT.

> **Wiring gap, not a wall — two lines close it.** (i) A fourth `IActorStatSubsystem` that folds active
> status `StatMods` on derived channels into `ActorHub.ResolveDerived`, mirroring `AtomDerivedSubsystem`'s
> shape exactly. (ii) A per-round `RecomposeDerived` call in Battle. Neither needs a new atom kind, a new
> trigger, or a vocabulary change.

**Continuous vs discrete — the one genuinely missing piece.** A *stacking* status gives a discrete ramp
(`StatusStacking { Refresh, Replace, Coexist }`, `src/FusionRpg.Core/Status/ResistanceEvaluator.cs:18-23`).
A *continuous* read — "+1‰ damage per 1‰ missing HP" — has no shape in the vocabulary: a value spec is a
number or a curve, and predicate leaves are booleans. Discrete stacks are the honest answer, and they are
also the bounded one (§5). INFERENCE.

**Helps a focused build? Yes.** A ramp bought once converts a corner's *wasted* marginal magnitude into
something that grows during the fight it is currently losing. It also has a natural asymmetry: a build
that takes lots of damage (a corner, by §2.2) ramps faster than one that takes little (a spread build).

---

### M2 — Conversion / reroute

**What:** a damage or defensive quantity is re-typed rather than resized — an attack's element converted,
damage taken rerouted to a pool, physical taken as elemental.

**Today: the resolver expresses it perfectly; nothing authors it from a passive.** `ElementPayload` is a
weighted component list parsed at `OverlayCombatCalculator.cs:357` and validated to sum to 1.0
(`ElementPayload.Validate`, called `:77`), and the matchup, defense, penetration, crit and shield-relation
reads are all **per component** (`:128-173`, `ShieldMath.cs:105-118`). So changing the payload genuinely
rewrites which channels the hit meets — which is exactly what
[passive-tree-ideal.md](../../architecture/passive-tree-ideal.md) D16 demands ("conversion nodes rewrite
element payload tags, not just magnitudes"). FACT.

**But no atom kind writes `packet.ElementPayload`.** The payload comes from whatever built the packet.
None of the 16 kinds (`AtomKindRegistry.cs:476-869`) has a packet-shaping parameter. This is attach point
(B), and (B) has no passive vehicle.

**Verdict: new capability at the atom layer** — a packet-shaping kind — *or* content authoring (the
action carries the payload, and the passive picks the action). The second costs nothing and should be
tried first. Note the honest framing: this is not a resolver limitation. RECALL for D16; FACT for the rest.

**Helps a focused build? Neutral.** An element-focused build gains; an aptitude corner does not.

---

### M3 — Threshold trigger

**What:** a discrete effect that fires when a state crosses a line. "Below 30% HP, gain a shield." "The
first hit each wave is negated." "Every fifth hit."

**Today: fully expressible.** Every part is built and has a consumer:

- Condition: `HpBelowMilli` / `HpAboveMilli` / `HasStatus` leaves with real `FactReader` readers
  (`PredicateNode.cs:17-31`, `FactReader.cs:69-100`). FACT.
- Trigger: `OnDamageTaken` / `OnDamageDealt` / `OnDeath` / `OnTimer` / `OnActivate`
  (`AtomKind.cs:83-95`; the registry's `AllTriggers` set is `AtomKindRegistry.cs:46-48`). FACT.
- Writer: `shield.grant` (`AtomKindRegistry.cs:650`), `status.apply` (`:610`), `resource.delta` (`:541`),
  all carrying `AllTriggers`. FACT.
- Rate limiting: `AtomRunner` implements `icd`, `charges`, `everyHits` and `capPerMatch`
  (`src/FusionRpg.Core/Effects/Atoms/AtomRunner.cs:33-39`). FACT.

**Helps a focused build? Yes.** A threshold effect is **layer-independent** — it does not need a second
defensive axis to be worth anything, which is precisely what a corner build cannot afford. And it fires
more often for a build that is losing HP fast, which by §2.2 is the corner.

**Caveat, stated plainly:** the atom **pool is empty**
([atom-catalog-ssot.md](../../architecture/effect-atom/atom-catalog-ssot.md) §0, RECALL — *"the
vocabulary is closed and built. The POOL is empty."*). "Expressible today" means the machine runs it, not
that content exists.

---

### M4 — Resource trade

**What:** spend a pool to buy a combat effect. "Spend spirit to ignore the next hit."

**Today: WIRING GAP.** `resource.delta` accepts all six resource ids
(`AtomKindRegistry.cs:541`, vocabulary at `:104-108`), but that same declaration says *"only `hp`
executes today"*, and the closed form independently agrees: `DominanceGuard.BuildReservedFamilies`
reserves `resource.efficiency.*` for every id and `resource.max/regen/restore.*` for all five non-hp ids,
with the comment *"efficiency has none at all until the action-cost layer ships"*
(`src/FusionRpg.Core/Balance/Guards/DominanceGuard.cs:101-114`). Two independent sources, both code. FACT.

**Verdict: wiring gap, blocked behind the action-cost layer**, which is a separate unbuilt program.

**Helps a focused build? Neutral to weak.** A resource trade converts one axis into another, which is
what a hybrid already has for free.

---

### M5 — Denial of an opponent's layer

**What:** turn a defensive factor **off**, rather than reducing it. "Your parry does not apply to me."

**Today: GENUINELY NEW CAPABILITY.** This is the one entry in this document where the framing rule's
question 3 is reached, and the evidence is that **every shipped "breaks their X" channel is a saturating
contest that provably never reaches zero**:

| Breaker | Bound | `file:line` |
|---|---|---|
| `combat.penetration.*` | `pierceFactor` bounded **(0,1]** — "penetration can push defense arbitrarily close to zero but never below it" | `OverlayCombatCalculator.cs:138-141`, `:376-383` |
| `combat.parry.shred` / `combat.block.shred` | `ClampedContest.Apply` clamps to `[floor, cap]`; the **band still fires** even at full shred | `ClampedContest.cs:40-46`, `OverlayCombatCalculator.cs:254-266` |
| `combat.parry.break` / `combat.block.break` | `Math.Max(0.0, rate − break)` — floors the *rate* at 0, but the avoidance band cap (950‰) is independent | `OverlayCombatCalculator.cs:183-185`, `:344-355` |
| `combat.shield.pen` | capped at `PenCapKPm` (3000‰) — "penetration at best triples shield burn" | `ClampedContest.cs:36-38` |
| `combat.reflect.resist.*` | `Math.Clamp(..., 0.0, 1.0)` on rate and share | `CombatDamageDispatcher.cs:100`, `:104` |

FACT throughout. Every one is a *magnitude in a contest*. None is a switch.

**Where it would attach.** There is a shipped precedent for a **packet-scoped resolution rule**:
`CombatProfile`. `OverlayCombatRequest.Profile` selects host resolution behaviour
(`OverlayCombatCalculator.cs:46`) and `Profile.MinChipShareKPm` already changes what the resolver does
per host (`:288-294`, dead on the Overlay profile at 0). A denial mechanism would be the same shape: a
packet-scoped flag that zeroes exactly one named factor for exactly one hit.

**Helps a focused build? Yes, more than anything else here.** §3.5's diagnosis is that *"every opponent
finds an open one"*. Denial is the direct inverse — it lets a focused build close a layer it never bought
by removing the opponent's use of it, once, occasionally. Bounding it is §5's job and is not hard.

---

### M6 — Stacking / decay change

**What:** change how long, how strongly, or how many times a state persists.

**Today: yes for status, partial for shield.** `status.duration.*`, `status.durationReduction.*`,
`status.intensity.*`, `status.intensityReduction.*` are registered channels
(`src/FusionRpg.Core/Stats/Derived/DerivedStatChannels.cs:442-465`) and `StatusStacking` is a def property
(`ResistanceEvaluator.cs:18-23`). Shield has `combat.shield.regen.*` (`DerivedStatChannels.cs:105`),
priority ordering and `RefillOnMerge` (`ShieldGate.cs:41-46`, `ShieldRuntime.cs:152`, `:191-202`). FACT.

**Helps a focused build? Only conditionally.** It is a *depth* mechanism: it makes an axis the build
already owns last longer. A Vigor or status corner gains; a Might corner gains nothing. **Partially
targeted — fails the acceptance test on its own, useful as a tier-2 node inside a tree that already has
one of §4's answers.**

---

### M7 — Retaliation

**What:** hitting you costs them.

**Today: BUILT AND LIVE, including the production caller.** `TryReflect`
(`CombatDamageDispatcher.cs:85-123`) resolves a reflect roll and a reflect share from four channels
(`DerivedStatChannels.cs:150-157`, read at `CombatDerivedReader.cs:69-72`), builds a reversed packet and
re-enters the dispatcher. The injector threads the required `actorResolve` at
`src/FusionRpg.Injector/Effects/EffectRuntime.cs:491` — whose own comment records that this was the fix:
*"it shipped with the math but no production caller ever passed this argument."* FACT. That is a wiring
gap that has already been closed; do not re-report it as one.

**Bounds, already in code:** `pReflect` clamped `[0,1]` (`:100`), `reflectShare` clamped `[0,1]` (`:104`),
bounce chain bounded by the shared `ProcDepthLimit` (`:28`; `combat.v1.json procDepthLimit: 6`), terminal
bounces **dropped rather than applied at a clamped zero** (`:59-63`). FACT. A reflect build is therefore
already provably incapable of running away.

**Helps a focused build? Yes — for a defensive corner specifically.** A Bulwark/Fortitude corner's hole is
that it has no offence. Reflection converts its one maxed axis into damage, without buying a second axis.
That is a genuine focus-build rescue and it is the cheapest one available. Worth nothing to Might.

---

### M8 — Cost-structure change

**What:** change what an action costs, how often it fires, or how much it delivers per cast.

**Today: WIRING GAP, and the inert line is documented in the resolver itself.**
`OverlayCombatRequest.EffectivenessMultiplier` defaults to `1.0` and its own doc says *"no current caller
sets this (the action system that would resolve 'which category, whose snapshot' is still being
specified), so every shipped call site is byte-identical"*
(`src/FusionRpg.Core/Combat/OverlayCombatCalculator.cs:14-22`). The channel family
`skill.effectiveness.{category}` is registered (`DerivedStatChannels.cs:483`, `:486`) and the conversion
helper exists (`:37`). `skill.cooldown.{category}` likewise. Both families are in `DominanceGuard`'s
reserved list (`DominanceGuard.cs:116-120`). FACT.

**Verdict: wiring gap behind the action layer**, same blocker as M4.

**Helps a focused build? Neutral.** It scales what you already have — which §3.5 already proved is the
wrong lever.

---

### M9 — Timing change

**What:** act more often, act first, act out of turn.

**Today: expressible on the lawn.** `attackInterval`, `attackSpeedAdder`, `attackCountdown`,
`plantSpeed`, `zombieSpeed` are primary channels (`ModifierOp.cs:41-66`), writable by `stat.modify` under
every trigger (`AtomKindRegistry.cs:476-503`). Battle has locked round order and initiative
(`BattleEngine.cs:16-18`). FACT.

**But it is invisible to the measurement.** `Predictor` models exactly one swing per side per round
(`Predictor.cs:161-171`), so an attack-speed mechanism does not appear in the win share at all. This is a
**measurement gap, not a design gap** — and it is the clearest single reason the closed form alone cannot
score mechanism nodes (§6).

**Helps a focused build? Weak, and unmeasurable today.** More swings multiply the same product from §2.1;
they do not fill an open factor.

---

### M10 — Layer piercing / bypass

**What:** the attacker's mirror of M5 — an attack that *ignores* a defensive factor rather than
out-scaling it.

**Today: reduction yes, bypass no.** Same evidence table as M5 — every piercing channel is a bounded
contest.

**One shipped near-bypass, found in the arithmetic and worth flagging.** A parried or blocked hit ends
resolution at `OverlayCombatCalculator.cs:236-266`: *"no block, no mitigation"* — the entire mitigation
chain never runs. So:

```text
D_parry  = max(0, base − removed),   removed ∈ [0, 950‰ · base]   →  D_parry ≥ 0.05 · base
D_clean  = max(0, (base+power) · K/(K+defense)) · ampFactor        →  D_clean → 0 as defense → ∞
```

`ClampedContest.Apply` floors `removed` at `floorKPm = 0` for parry/block
(`OverlayCombatCalculator.cs:260`, `:265`; `ClampedContest.cs:32-35`). **Therefore against a defender with
very high `combat.defense` and fully shredded `combat.parry.strength`, being parried deals MORE damage
than a clean hit.** INFERENCE from the arithmetic — not measured, and it should be. If it holds, the
resolver already contains an accidental anti-turtle punish that scales with exactly the layer a Fortitude
corner over-invests in. See §4c and §8.

**Helps a focused build? Yes.** A bypass is the offensive twin of M5's defensive answer.

---

## 4. The anti-spread mechanisms specifically

§3.5's diagnosis: *"a corner build maxes one axis and floors eleven, so every opponent finds an open
one."* Three replies are possible.

### 4a. Close eleven layers cheaply — the **layer-parity floor**

**Design.** One deep-tier node: *your weakest defensive channel reads at no less than `f` × your
strongest.* Every combat defensive family gets a floor derived from the actor's own allocation.

**Where it attaches.** It cannot be an atom — an atom writes a fixed value and does not read the actor's
other channels. It is a **new `IActorStatSubsystem`**, folded by `ActorHub.ResolveDerived`
(`ActorHub.cs:56-59`), registered exactly the way `AtomDerivedSubsystem` is (`ActorHub.cs:154-155`).
**No vocabulary change, no new attach point, no new trigger.** New code, existing architecture.

**Why it targets focus specifically.** Its value is proportional to the *spread* of the holder's own
allocation. For an even-twelve build it is identically zero — every channel already equals the maximum.
For a corner it is maximal. That is the acceptance test passed by construction. INFERENCE.

**Bound — the even-split ceiling (§5).** The floor is `min(f × maxChannel, evenSplitValue)`, where
`evenSplitValue` is what an even allocation of the actor's *own* total would give. The node can therefore
**never** hand a focused build more total defensive value than an equally-invested spread build already
has. That directly satisfies the owner's rule: an all-in attacker never ends up 2× or 10× a two-way
hybrid.

### 4b. Bypass a layer entirely — **the one-hit window**

**Design.** A deep node grants: *once per `N`, one hit resolves with exactly one named defensive factor
neutralised* — the defender's avoidance band, or their mitigation, or their crit-denial. One factor, one
hit, on an ICD.

**Where it attaches.** Packet-scoped, the `CombatProfile` shape (`OverlayCombatCalculator.cs:46`, `:288`).
Rate-limited by `AtomRunner`'s existing `icd` / `charges` machinery (`AtomRunner.cs:33-35`).

**Genuinely new capability** (M5/M10) — say so plainly. It is not a wiring gap; the resolver has no
switch, only dials.

**Bound.** It neutralises **one** factor of the six-way product in §2.1, for **one** hit, on a cooldown.
The worst case is arithmetically `1/f_i` of one hit's damage — and since every `f_i` is itself bounded
(logistic ≤ 1, `K/(K+d)` ≤ 1, avoidance band ≤ 950‰), the multiplier is bounded by construction, not by a
clamp. It cannot compound across factors because the node names exactly one.

### 4c. Punish breadth — **Erosion**, the anti-turtle design

This is `class-system-map` §4b's *"anti-turtle punish"*, made concrete.

**Design.** On a landed hit, apply a stacking status that subtracts a **flat absolute amount `E`** from
**every** defensive channel the defender holds — `combat.defense.omni`, `combat.dodge.omni`,
`combat.crit.resist.omni`, `combat.parry.rate.omni`, `combat.block.rate.omni`,
`combat.absorption.omni`, `combat.reduction.omni`, `combat.shield.toughness.omni`. Not a percentage. A
flat subtraction, applied uniformly across the whole defensive vector.

**Why a flat, uniform subtraction punishes breadth and not focus.** This is §2.2 run backwards, and it is
what makes the design principled rather than an ad-hoc counter:

- A **broad** defender sits every factor on the **steep** part of its curve — that is exactly *why*
  breadth wins. A flat subtraction there removes a large amount of expected mitigation from every one of
  eight factors, and the losses **multiply**.
- A **corner** defender has one factor deep in **saturation**, where a flat subtraction moves the factor
  almost not at all, and eleven already at floor, where the subtraction is absorbed by the channel's own
  registered default.

So the same `E` costs a spread build several times what it costs a corner. **INFERENCE** from §2.2's
shapes, and it is directly measurable (§6) — it should be measured before it is specced.

**Where it attaches.** `status.apply` on `OnDamageDealt` (`AtomKindRegistry.cs:610`, `AllTriggers`
`:46-48`), carrying a `ModifyStat` payload naming derived channels — a shape `StatusStatPayload` already
validates and whose worked example is a `combat.*` channel (`StatusStatPayload.cs:30-32`, `:123-128`).

**The one blocker, named:** as established in **M1**, a status's derived-channel `StatMods` are upserted
into the **primary** session bag (`EffectRuntime.cs:81`) and no registered `IActorStatSubsystem` reads
that bag (`ActorHub.cs:145-155`), so they never compose into `ActorDerivedSnapshot`. **This is a wiring
gap, not an architectural wall** — the fix is a fourth subsystem shaped like `AtomDerivedSubsystem`.

**Bounds, all three provable:**

1. **Per-stack:** `E` is a per-mille share of `P(Θ)`, so it rides the one power ladder and never outruns
   the scale it is subtracting from.
2. **Total:** every affected channel clamps at its own registered default — the worst case is *"the
   defender's defensive layers read like a corner build's floors."* **The design cannot make any actor
   worse than the weakest legal build.** That is a hard, stated bound and it is not a magnitude ceiling,
   so PS-8 is untouched.
3. **Direction:** Erosion **removes mitigation; it never adds damage.** So it cannot one-shot, and it
   composes with amplification only through the mitigated fraction, which is already bounded below by 0
   (`OverlayCombatCalculator.cs:269`, a structural floor, documented as such at `:276-281`).

**Why it is the top-ranked mechanism.** It is the only design in this document whose value is a function
of the **opponent's** shape rather than the holder's. It raises corner-vs-spread without raising
corner-vs-corner — which means it moves the exact cell §3.5 needs moved and leaves the dominance matrix
`balance-guard` already reports substantially alone. INFERENCE, and the single most important thing to
measure first.

**Free variant worth testing before building anything:** M10's parry-bypasses-mitigation observation. If
the arithmetic holds in a real run, the shipped resolver already punishes a mitigation turtle, and the
design question becomes "tune the existing effect" rather than "add a mechanism." One measurement, no code.

---

## 5. Bounded, not degenerate

The owner's rule, verbatim: *"you shouldn't make an actor who spends all resource to power attack be
stronger 2x or 10x than an actor who spent 2 hybrid."*

### 5.1 The governing principle — the even-split ceiling

**Proposed as the general bound for every mechanism node: the reference build is the even split, and no
mechanism may take a focused build past it on the axis the mechanism touches.** A mechanism may *close*
the gap to a spread build; it may not overshoot it.

This is checkable rather than argued, because the even-twelve build is already in the measured build list
(`tools/HybridViability/Program.cs:114-115`), and it is exactly the acceptance criterion §3.5 wants: the
target is "focus is competitive", never "focus wins."

### 5.2 Per-mechanism bounds

| Mechanism | Bound | Why it cannot run away |
|---|---|---|
| **M1 Conditional scaling** | Discrete stacks with a declared max; magnitude per stack reads `P(Θ)` | Stacks are finite and the per-stack value rides the one ladder. A *continuous* read is refused precisely because it has no natural bound |
| **M3 Threshold** | `AtomRunner` `icd` / `charges` / `capPerMatch` (`AtomRunner.cs:33-39`) | Already-shipped rate limiting, per binding, per match |
| **M5 / M10 Denial & bypass** | One named factor, one hit, on an ICD | Each `f_i` is itself bounded (logistic ≤ 1; `K/(K+d)` ≤ 1; band ≤ 950‰), so removing one is bounded by construction. Naming exactly one factor prevents compounding |
| **M7 Retaliation** | Already bounded in code: rate and share both `Math.Clamp(…, 0, 1)`, chain bounded by `ProcDepthLimit` | `CombatDamageDispatcher.cs:100`, `:104`, `:28` |
| **§4a Layer parity** | `min(f × maxChannel, evenSplitValue)` | Provably ≤ what an even build with the same points already holds — the even-split ceiling, stated as arithmetic |
| **§4c Erosion** | Flat per-stack, channels clamp at their registered defaults, removes mitigation only | Worst case is "defender reads like a corner build's floors" — cannot go below the weakest legal build, and cannot add damage |

### 5.3 The two repo rules these must respect

- **Absolute bounds throw; they never clamp silently.** The shipped precedent is `ShieldMath.MaxInput`,
  derived from the loaded policy coefficients and throwing `ShieldInputOverflow` rather than clamping
  (`src/FusionRpg.Core/Combat/Shield/ShieldMath.cs:30-63`, `:76`). FACT. Any mechanism magnitude that can
  overflow gets the same treatment.
- **No hard progression ceilings.** Note carefully which bounds above are exempt and why: the avoidance
  band cap, the pen cap and the reflect clamps are **bounded ratios**, which
  [ssot-power-scale.md](../../architecture/power/ssot-power-scale.md) §11 exempts. The even-split ceiling
  is a **relative** bound between two builds at the same investment, not a ceiling on either — it does not
  stop either build growing. That distinction is load-bearing and must be stated in any spec that adopts it.
  RECALL for §11's exemption; FACT for the shipped clamps.

---

## 6. Measurability — what would score a mechanism node

§3.5 is right that mechanism nodes are *"outside its saturating ratio math by construction."* But the
reason is narrower than it sounds, and that narrowness is the recommendation.

### 6.1 What the closed form already scores for free

`StrikeMixture` does not re-implement combat math — it **calls the shipped functions**
(`StrikeMixture.cs:16-20`, whose own doc says *"A change to any of those shipped functions moves this
module's prediction automatically"*). It calls `PierceFactor`, `DivisiveMitigation`, `AmpFactor`,
`AmpFactorReciprocal`, `CapAvoidanceBand`, `CombatProbability.Sigmoid` and `ClampedContest.Apply`
(`StrikeMixture.cs:64-118`). FACT.

**So any mechanism implemented as a change to those functions is scored with zero harness work.** That
covers §4b's bypass and §4c's Erosion, if Erosion is expressed as a snapshot difference — which it is.

### 6.2 What the closed form cannot see, and why

| Blind spot | Cause | `file:line` |
|---|---|---|
| Anything triggered per hit | `Predict` models one swing per side per round | `Predictor.cs:161-171` |
| ICDs, charges, stacking over time | No time axis for grants; status is a single profile applied once | `Predictor.cs:44-52`, `:110-126` |
| Timing / attack speed (M9) | Same one-swing-per-round assumption | `Predictor.cs:161-171` |
| Any authored base hit | Every measured actor is built with `BaseDamage: 0.0, ShieldMaxHp: 0` | `TerminationGuard.cs:123` |
| Elements | Deliberately neutralised, and the guard reports it | `StrikeMixture.cs:22-24`, `DominanceGuard.cs:82` |
| Resources, actions, cooldowns | Explicitly reserved | `DominanceGuard.cs:101-120` |

### 6.3 What already exists that nobody has wired

- **`tools/CombatSim/Simulator.cs`** drives the real `CombatDamageDispatcher.DispatchInstant` over
  `FoundationHarness`, with the shield gate and reflection on, reading results back out of the Funnel
  (`Simulator.cs:49-52`, `:59-110`). It is trial-based and seeded with two independent streams (`:61-64`).
  FACT.
- **`BattleEngine`** is a pure deterministic resolver — *"No I/O, no clock, no ambient state: same setup +
  seed + platform ⇒ byte-identical report"* — running the SSOT resolver and `DamageApplyPipeline`
  (`src/FusionRpg.Core/Battle/BattleEngine.cs:10-20`). FACT.
- **Neither is reachable from `DominanceGuard.Measure`**, which builds actors via the `internal`
  `TerminationGuard.ToActor` and calls `Predictor.Predict` (`DominanceGuard.cs:44-57`). FACT.

### 6.4 Recommendation

**Do all three, in this order. The split is by mechanism class, not by preference.**

| Step | What | Effort | Unblocks |
|---|---|---|---|
| **1** | **Implement mechanism nodes as changes to the shipped resolver functions wherever possible.** No harness work at all | **zero** — it is a design constraint, not a task | §4b bypass, §4c Erosion, M5/M10 |
| **2** | **Extend the closed form with two phases.** `PhaseModel.ShieldEffectiveHp` is the pattern (`Predictor.cs:130-135`): a threshold effect is a second phase, a ramp is a mean shift over the fight. Change `TerminationGuard.ToActor:123` to accept a real `BaseDamage`/`ShieldMaxHp` — that one line currently forces every measurement to a zero-base hit | **~1 focused session.** Two `PhaseModel` functions, one composition change in `Predictor`, re-bless goldens. Main risk is variance composition — `Predictor.cs:93-96` already notes DoT is folded mean-only | M1 (as a two-phase approximation), M3 |
| **3** | **Add a trial-based sibling to `DominanceGuard`** that keeps the same `Measure(builds, theta) → DominanceReport` signature but resolves each arrow by Monte Carlo over `BattleEngine`, not `Predictor` | **~1–2 sessions.** The engine exists; the work is a build→setup conversion, a trial count with a convergence check, and a determinism assertion. `ToActor` being `internal` (`TerminationGuard.cs:111`) needs one visibility decision | M9, ICDs, charges, stacking, everything per-hit |
| **4** | **Make Battle fire `OnDamageTaken` / `OnDamageDealt`.** Grep over `src/FusionRpg.Core/Battle/` returns **zero** hits for either, while the lawn fires both (`EventDrainHost.cs:95-99`) | **Its own piece of work**, not part of the harness. Scope it separately | Any trigger-driven mechanism node, in Battle |

**Verdict, stated plainly: a full battle simulation does not need building — two already exist. The
closed form should be extended for phases, not retired. The actual blocker is step 4, and it is a wiring
gap, not a simulator gap.**

**Do not let step 2 become the whole answer.** Its output is a *deterministic mean*, and a mechanism node's
whole point is often variance — a threshold that fires only when you are losing. A mean-only extension can
report "no change" for a node that materially changes how often you survive. Trials are not optional for
M3, M5 and §4b.

---

## 7. The ranking, restated with its reasoning

| Rank | Mechanism | Focus value | Buildable | Net |
|---|---|---|---|---|
| **1** | **§4c Erosion** (anti-turtle) | ★★★★★ — the only mechanism whose value reads the *opponent's* breadth | ★★★☆☆ — one named wiring gap (`ActorHub.cs:145-155`, no subsystem reads the session bag) | **Highest. Measure first, spec second** |
| **2** | **M7 Retaliation** | ★★★★☆ — rescues a *defensive* corner completely; worthless to Might | ★★★★★ — built, live, production caller wired (`EffectRuntime.cs:491`) | **Ship content today** |
| **3** | **M3 Threshold trigger** | ★★★★☆ — layer-independent, and fires more for a build that is losing | ★★★★★ — leaf, trigger and three writers all present with consumers | **Ship content today** |
| **4** | **M1 Conditional scaling** | ★★★★★ — `class-system-map` §4b's own first named fix | ★★☆☆☆ — two wiring gaps (`AtomKindRegistry.cs:535`; `BattleRunState.cs:124-127`) | **Two small wirings, then high value** |
| **5** | **§4a Layer parity** | ★★★★★ — attacks §3.5's stated cause head-on, zero value to an even build by construction | ★★☆☆☆ — new `IActorStatSubsystem`; no vocabulary change | **New code, existing architecture** |
| 6 | §4b Bypass / M5 denial | ★★★★★ | ★☆☆☆☆ — genuinely new capability | Highest ceiling, highest cost |
| 7 | M6 Stacking / decay | ★★☆☆☆ — only helps a build that already owns that axis | ★★★★☆ | Good tier-2 filler |
| 8 | M2 Conversion | ★★☆☆☆ | ★★☆☆☆ — no passive attach point | Try content authoring first |
| 9 | M8 Cost-structure | ★☆☆☆☆ | ★☆☆☆☆ — blocked on the action layer | Deferred |
| 10 | M4 Resource trade | ★☆☆☆☆ | ★☆☆☆☆ — 5 of 6 resources inert | Deferred |
| 11 | M9 Timing | ★☆☆☆☆ | ★★★☆☆ on the lawn, ☆ measurable | Unmeasurable until step 3 |

---

## 8. Open questions

1. **Does a parried hit already out-damage a clean hit against a mitigation turtle?** M10's arithmetic
   says yes (`D_parry ≥ 0.05·base` versus `D_clean → 0`). One measurement settles it, and if it holds the
   resolver already contains an accidental anti-turtle punish that the design should tune rather than
   duplicate. **Cheapest item on this list.**
2. **Does Erosion actually cost a spread build more than a corner?** §4c's claim is INFERENCE from the
   curve shapes. It is measurable through step 1 of §6.4 with no harness change — implement it as a
   snapshot difference and `StrikeMixture` scores it automatically. **Measure before speccing.**
3. **Should the even-split ceiling (§5.1) be a stated design law or a per-node bound?** It is a clean,
   checkable rule, but it is also a *relative* constraint between builds — which needs its own row in
   `decisions.md` before it can be treated as binding, and needs saying explicitly that it is not a PS-8
   ceiling.
4. **`TerminationGuard.ToActor` is `internal` and hardcodes `BaseDamage: 0.0`** (`TerminationGuard.cs:111`,
   `:123`). Every measurement in this program is a zero-base-hit duel. Is that intended scope, or a
   convenience that has quietly become the model? It matters: with base damage at zero, `combat.power` is
   the *entire* offence, which flatters Might and starves `skill.effectiveness` of any role at all.
5. **`DESIGN-GATE.md` §1's atom row says 8 triggers; `AtomKindRegistry.TriggerCount` is 13**
   (`src/FusionRpg.Core/Effects/Atoms/AtomKindRegistry.cs:36`, with `AtomTriggers.All` listing 13 at
   `AtomKind.cs:97-101`). E34 added five and the gate row was not propagated. **Code beats docs** — the
   gate row is stale, and the gate itself asks for corrections to be propagated (evidence rule 6).
6. **Does the resolver's purity (§1.0) need an explicit ADR row?** Everything in this taxonomy hangs on
   "there is no mid-hit callback." That is true today by construction, and it is the kind of fact a future
   session will re-derive expensively or, worse, quietly break.

---

## 9. Design-gate checklist

```
[x] I identified the subsystem(s) this touches — combat resolver, effect atoms, status, shield,
    element hub, class system, balance guards.
[x] I read every doc in the §1 row(s) for those subsystems, this session:
    DESIGN-GATE.md, combat-damage-ssot.md (§6-§9), effect-funnel.md (headings), element-hub-ssot.md
    (headings), status-ssot.md (headings), effect-atom/definitions.md (§3, §4, §8, §14.2),
    effect-atom/atom-catalog-ssot.md (§8, §8a), passive-tree-ideal.md (in full),
    class-system-map.md §4b/§4c, class-system-ideal.md §4.1/§4.2, decisions.md (class-system row).
[x] I checked decisions.md for a lock covering this — the class-system row (2026-08-26) locks the two
    acceptance criteria and states the dominance matrix is SOFT and "red by design today"; nothing
    there locks mechanism nodes.
[x] Every factual claim cites file:line.
[x] I verified claims against CODE, not comments — specifically: the status→derived compose gap was
    traced through StatusStatPayload -> EffectRuntime -> StatSystem.Resolve -> EntityFinal ->
    ActorHub.ResolveDerived -> the three registered subsystems, not inferred from any doc.
[x] I read the surrounding section of every rule I quoted.
[~] I tested (not assumed) any constraint I am reporting. PARTIAL, and stated: I ran no suite and no
    tool. Every "expressible / inert" claim is read from code and cited; every behavioural claim about
    what a mechanism WOULD do to win share is tagged INFERENCE and listed in §8 as measurable. In
    particular §4c's core claim and M10's parry observation are unmeasured.
[x] Nothing contradicts a §2 invariant. §1.0's "no mid-hit callback" is a restatement of
    record-then-drain (invariant 2) and deltas-not-absolutes (invariant 3), not a contradiction.
[ ] Corrections propagated. NOT DONE, deliberately — this is a research file and I was asked not to
    modify src/, tools/, tests/ or data/. Two propagations are owed and named in §8: DESIGN-GATE.md's
    stale 8-trigger count (item 5), and passive-tree-ideal.md §3.5 gaining a pointer to this file.
```
