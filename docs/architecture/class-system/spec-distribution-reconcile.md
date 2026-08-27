# Spec: `distribution-reconcile` — the primary → derived path is stubs, end to end

**Module id:** `distribution-reconcile` · **Program:** [class-system-map.md](../class-system-map.md) ·
**Status: AUTHORIZED 2026-08-26 -- owner's /goal directive commands execution of the class-system plan to completion; supersedes this "awaiting owner review" header, which was never flipped after that directive landed.**

**Depends on:** nothing · **⛔ Blocks `aptitude-resolve` hard** — without it that module's central test is
red by construction

---

## 1. Objective

**Owner, 2026-08-26:** *"ClassStatPlugin already exists → they are stub, need reconcile."*
*"BattleStatComposer → stub too, our primary stats and derived stats distribute need reconcile all
blast."*

Both correct, and the sweep they prompted found the problem is not two stubs. **The entire primary →
derived distribution path is stubs, seams that were reserved and never wired, or code whose own
comments say it is inert.** This module surveys that path and gives every item a verdict — **fill,
wire, replace, delete, or document** — before anything in this program tries to stand on it.

**This module adds no mechanic.** Like [derived-stats/spec-unbuilt-reconcile.md](../derived-stats/spec-unbuilt-reconcile.md),
it only removes disagreement. The difference is that one reconciled *specs*; this one reconciles
**shipped code against the specs that describe it**.

**And a verdict is allowed to be "leave it, deliberately".** §3.2 is the case: the two composers stay
separate because the stream that owns the file already decided so, and because unifying them would
destroy another program's proof. **Reconcile means every item has a recorded answer — not that every
item changes.**

**Users:** `aptitude-resolve` above all; every module downstream of it.

**Success is measurable:** an aptitude-sourced `DerivedModifier` reaches a composed channel value **on
both the overlay path and the battle path**, with a non-zero `Θ`, and a test proves the two agree.

---

## 2. ⛔ It corrects this program's own spec, written earlier today

[spec-aptitude-resolve.md](spec-aptitude-resolve.md) §2 said the seam already existed and the module
should simply fill `ClassStatPlugin.Contribute`. **That is wrong, and the sweep is why.** The
correction is recorded here rather than quietly edited, because the mistake is instructive: the stub
was *found*, its registration was *verified*, and it still was not the right seam. **Finding a seam is
not the same as reading what flows through it.**

---

## 3. The register — nine items, each with evidence and a verdict

### 3.1 ⛔ `ClassStatPlugin` is the **wrong pipeline**, not an empty one

There are **two** modifier pipelines and they do not meet:

| Pipeline | Contract | Carries | Composed by |
|---|---|---|---|
| **Primary** | `IStatModifierPlugin.Contribute(StatContext, IModifierBagEditor)` → `Upsert(StatModifier)` | `StatModifier` | `StatComposer` → `EntityFinal` |
| **Derived** | `IActorStatSubsystem.ContributeDerived(StatContext, ICollection<DerivedModifier>)` | `DerivedModifier` | `DerivedComposer` → `ActorDerivedSnapshot` |

`ClassStatPlugin` ([StubStatPlugins.cs:3-9](../../../src/FusionRpg.Core/Stats/Plugins/StubStatPlugins.cs))
is on the **primary** side. It is genuinely registered — `StatSystemBootstrap.cs:17` — and a test even
pins its order (`StatSystemTests.cs:205`). But **aptitudes feed 83 derived channels** (ideal §4.2), and
a derived channel is unreachable from a `ModifierBag`.

> **Verdict: the aptitude seam is `IActorStatSubsystem`, registered via `ActorHub.Register`** —
> the same door `RpgProgressionSubsystem` already uses
> ([ActorHub.cs:31-37, 53-61](../../../src/FusionRpg.Core/Stats/Derived/ActorHub.cs)).
>
> **Verdict: KEEP IT, with a two-part comment — decided 2026-08-26 after the owner asked what it
> actually does.** An earlier draft of this section said *delete*. Reading further found a real future
> job.

**Why not delete.** A filled `ClassStatPlugin` reaches `EntityFinal`, and that includes
**`AttackInterval`, `ProduceInterval`, `ZombieSpeed`** — genuine composable channels
(`StatChannels.AttackInterval`, composed at [StatComposer.cs:106](../../../src/FusionRpg.Core/Stats/StatComposer.cs),
with an inverted grammar: `ModifierOp.cs:69` marks intervals `ChannelDirection.LowerIsBetter`).

**Ideal §4 slates `Agility` to carry `turn.*` "when registered"** — the battle-side speed channels.
**Its PvZ-overlay analogue is `attackInterval`, which is primary-only and unreachable from a derived
subsystem.** So the day an aptitude contributes attack speed in overlay mode, this plugin is the only
door, and deleting it means re-adding it.

> **The comment must name both halves:** what it is for (a future primary-channel contribution such as
> `attackInterval`) and what it is **not** for (aptitude derived edges — those go through
> `IActorStatSubsystem`). That removes the misleading-empty-seam problem, which is real — it misled
> this program's own spec — without discarding a seam that has a named use.

**One more thing worth recording, because it makes `Order` legible.** `StatComposer` is phased: Flat
**sums**, Increased **sums**, More **multiplies**, Override takes highest `Priority`. All commutative —
so **plugin `Order` does not change the composed value.** It decides exactly one thing:
`ModifierBag.Upsert` does `_byKey[mod.Key] = mod`, so a later plugin **overwrites** an earlier one on a
key collision. `Order` is **collision precedence, not a math sequence** — `rpg.class` at 100 losing to
`cheat.absolute` at 950 is the intended relationship.

`AchievementStatPlugin`, `ItemStatPlugin` and `BuffStatPlugin` are the same shape and the same question.
**Not this program's to answer** — flagged so the achievement, item and buff programs get told, not
silently inherited.

### 3.2 ⛔ `BattleStatComposer` runs **no subsystems at all**

Verified by reading the whole method
([BattleStatComposer.cs:88-145](../../../src/FusionRpg.Core/Battle/BattleStatComposer.cs)). It never
references `ActorHub`, `DerivedComposer` or `IActorStatSubsystem` — grep over
`src/FusionRpg.Core/Battle/` returns **one** hit, and it is a doc comment. It builds the snapshot
directly:

```text
ActorDerivedSnapshot.FromValues(5 channels from BattleRuleset level formulas)
  + AddAffinity(primary element)  + AddAffinity(secondary)
  + traits.ModsFor(traitId)       <- additive
  + setup.ChannelMods             <- additive, validated against KnownChannels
```

Its own summary calls it *"the web-mode analogue of the ActorHub compose path"* — **analogue, not the
same path.**

> **So an aptitude subsystem registered on `ActorHub` is invisible to battle**, and
> `spec-aptitude-resolve.md`'s central test — *both composers resolve the same values* — **fails by
> construction** rather than by a bug.

#### Verdict: ⛔ **do NOT unify the composers.** Use `ChannelMods`, the shipped pattern for this exact problem

**Owner, 2026-08-26:** *"battle have a plan to refactor with some ideal — should we fix it or fix the
documents?"* **Read their plan; the answer is neither, and the repo already solved this.**

**1. `ChannelMods` is the seam, and a progression system already uses it for precisely this.**

```csharp
// StarPolicy.cs:6
/// ChannelMods - never engine changes (battle goldens stay byte-identical).
```

Four producers fill `BattleActorSetup.ChannelMods` today: `TraitAtomSource` / `TraitBattleCatalog`,
`StarPolicy.StarChannelMods`, [ExpeditionResolver.cs:246-250](../../../src/FusionRpg.Core/Expeditions/ExpeditionResolver.cs),
and [WebMatchService.cs:281](../../../src/FusionRpg.Server/WebMatchService.cs). **Star rank is a
progression system contributing stats to a battle actor without touching the engine** — the same shape
as an aptitude allocation. **Aptitudes become the fifth producer, and `BattleStatComposer` is not
modified at all.**

**2. The battle stream is not going to unify them, and their own spec says why.**
[battle-timeline-map.md](../battle-timeline-map.md)'s scope boundary does not list stat composition in
scope. And T3 hit **this exact divergence** and chose to keep it —
[battle/spec-readiness-model.md](../battle/spec-readiness-model.md):

> *"Battle-only use works through `BattleStatComposer`'s **separate known-channel set**, but that is
> not the same as being a real stat."*

Their remedy was to register `turn.*` in `DerivedStatRegistry` **and** add it to the known-channel set —
**both, not merged.** That is the precedent, from the stream that owns the file.

**3. Unifying would destroy T5's proof.** `T5 kernel-adoption` is *"the gate: byte-identical"* — four
battle goldens, four expedition goldens, `RulesetVersion` stays 2, *"any drift is a bug"*.
[decisions.md](../decisions.md)'s *Golden ordering across streams* row: **"freeze first, move last"** —
*"if a mover overlaps a freezer, neither can attribute a hash change to its own work and the freezer's
proof is worthless."* Changing what a battle actor composes is a mover.

**4. And that file has already refused a fourth path once.** Its E12 comment: *"an earlier draft had
battle reading bindings in three places, which would have been a fourth path bypassing both the
compiler and the runner and appearing in no spec."*

> **So: fix the documents — including this spec's own earlier verdict, which said WIRE — plus one
> narrow code item (§3.2a). The composers stay separate, deliberately, and that is now a recorded
> decision rather than an unexamined divergence.**

#### 3.2a The one real code item: the known-channel set is narrower than the distribution

```csharp
// BattleStatComposer.cs:46-61
static HashSet<string> BuildKnownChannels()
    => new(DerivedStatChannels.AllCombatChannelIds) { StatusPower{Omni,Dot,Cc,Contagion},
                                                      StatusResist{Omni,Dot,Cc,Contagion} };
// :129-132
if (!KnownChannels.Contains(mod.ChannelId))
    throw new ArgumentException($"Unknown combat channel id '{mod.ChannelId}'.");
```

Aptitude edges reach channels **outside** that set — `resource.max.*` and `resource.regen.*` (which
[resource-hub-ssot.md](../resource-hub-ssot.md) §8 states *"form their own family list and do not join
`CombatChannelFamilies`/`AllCombatChannelIds`"*), plus `skill.cooldown.*`, `skill.effectiveness.*`,
`move.range`, `progression.*`, and the `status.duration.*` / `status.intensity.*` families
`derived-stats` added after this set was written.

**⛔ Measured 2026-08-26, and it is far larger than "a narrow item":**

```text
84  channels named by the shipped aptitude edges
 0  unregistered           <- every one IS in the catalog; aptitude-tuning test 4 passes
47  outside BattleStatComposer's known-channel set   <- 56% of the distribution
```

By family: `progression.bonus` 5 · `resource.max` 5 · `resource.regen` 5 · `skill.cooldown` 5 ·
`skill.effectiveness` 5 · `status.duration` 4 · `status.durationReduction` 4 · `status.intensity` 4 ·
`status.intensityReduction` 4 · `resource.efficiency` 3 · `progression.*` 2 · `move.range` 1.

**So a `ChannelMod` on any of them throws today** — a loud `ArgumentException`, which is the good
failure, and exactly the gap T3 found for `turn.*`. **But over half of what an aptitude buys is
unreachable on the battle path**, which makes this the largest item in the register rather than a
footnote. An earlier version of this section listed the families without counting them.

> **Sequence this item immediately after `unit-class-close`** (decided 2026-08-26). That module reads
> the consumers of the same families — `resource.*`, `skill.*`, `move.range`, `progression.*`,
> `status.duration/intensity.*` — to assign a `unitClass`. **Consume its readings; do not repeat
> them.** The two stay separate modules because they gate different things (`unitClass` blocks
> `aptitude-tuning`; this set blocks `aptitude-resolve`), but reading a consumer twice is waste.

> **The repair is T3's, verbatim: widen the known-channel set to the channels the distribution
> actually touches.** Additive, value-changing for nobody, and testable as a set difference: *every
> channel named by an aptitude edge is in `BuildKnownChannels()`.* **That test is the deliverable**,
> because the set will keep drifting as families are added — which is how it drifted here.

### 3.3 ⛔ `Θ` is **zero** on the overlay path, and the code says so

```csharp
// CheatState.cs:30-32
/// <summary>Derived snapshot compose - wraps Stats; Writer uses AppliedCombat. Not yet fed by
/// PowerIndex - see RpgProgressionSubsystem's doc comment (power-plan.md T3.2).</summary>
public static ActorHub ActorHub { get; } = ActorHubBootstrap.CreateDefault(Stats);

// CheatState.cs:34-35
/// <summary>Theta ladder index, ready for a consumer (power-plan.md waves 2-3) - inert until then.</summary>
```

`CreateDefault(Stats)` passes **no** `IPowerIndexProvider`, so `RpgProgressionSubsystem` falls back to
`StubPowerIndexProvider` — **`Θ = 0`**
([RpgProgressionSubsystem.cs:34](../../../src/FusionRpg.Core/Stats/Derived/Subsystems/RpgProgressionSubsystem.cs)).
The server registers `ServerPowerIndexProvider` in DI (`Program.cs:101`) but for its own consumers, not
for an `ActorHub`.

**Why this is fatal to this program specifically, rather than merely untidy:** the magnitude read is
`k · share^γ · P(Θ)`. **At `Θ = 0` every magnitude edge collapses to `P(0) = C`** — the floor,
identical for every build. Contest edges still work (they are `Θ`-free by construction), so the
symptom would be *"defence and rates behave, every magnitude is flat"* — which reads exactly like a
coefficient problem and is not one.

> **Verdict: WIRE, and it is `aptitude-resolve`'s hardest precondition.** `PowerIndex` is described in
> its own comment as *"ready for a consumer… inert until then."* **This program is that consumer.**

### 3.4 `BattleStatComposer` aliases `Θ` to Level — the other half of 3.3

```csharp
// BattleStatComposer.cs:91-94
// setup.Index is Theta - an alias for Level, not a new source (no real power-index composition
// is wired through BattleActorSetup yet; that is a later wave's job).
int theta = setup.Index;
```

So the two paths obtain `Θ` **differently**: overlay from `IPowerIndexProvider` (inert), battle from a
level alias. Any test asserting the two composers agree must first make them agree about `Θ`.

> **Verdict: DOCUMENT the contract, WIRE only if 3.2 requires it.** The alias is honest and labelled;
> what is missing is a stated rule that both paths read one `Θ` source. Reconciling the *seam* without
> reconciling the *input* would leave a divergence that looks like model error.

### 3.5 ⛔ `progression.bonus.*` is a private `f(level)` **absent from the closed power inventory**

```csharp
// RpgProgressionSubsystem.cs:56-70
mods.Add(new DerivedModifier(ProgressionBonusMaxHp,   Flat, level * 10,  ...));
mods.Add(new DerivedModifier(ProgressionBonusAtk,     Flat, level,       ...));
mods.Add(new DerivedModifier(ProgressionBonusDefense, Flat, level * 0.5, ...));
```

Three magnitudes derived from a level, as bare literals, in a subsystem.

- [decisions.md](../decisions.md) *P2* knows it is a stub: *"Level-scaled stub in
  `RpgProgressionSubsystem` **until dedicated bonus ADR**."*
- **But it is not in [power/ssot-power-scale.md](../power/ssot-power-scale.md) §10.** Grepped this
  session: neither `progression.bonus` nor `RpgProgressionSubsystem` appears anywhere in that file.
  §10 says *"a power-shaped number that is not in this table does not have permission to exist"*, and
  the power audit's own G2 sweep caught `PatronPolicy.AuraMilli` (row 16) while missing this.
- **Latent, not live.** `_level` defaults to `null` → `level = 0` → early return, and no host passes a
  delegate (`CheatState.cs:32` passes only `Stats`). **It fires the moment one does.**

> **Verdict: REPLACE — and this program is P2's "dedicated bonus ADR".** Ideal §4 already assigns every
> one of these: `progression.bonus.atk` → `Might`, `maxHp` → `Vigor`, `defense`/`arm1`/`arm2` →
> `Fortitude`. So the stub curve is not merely retired, it is **superseded by an allocation**, which is
> the outcome PS-3 wants: no private level curve, magnitudes through `P(Θ)`.
>
> Whichever way it goes, **§10 gets a row or a deletion in the same change.**

### 3.6 The derived → primary bridge is exactly five channels wide

`MergeAppliedCombat` ([ActorHub.cs:64-88](../../../src/FusionRpg.Core/Stats/Derived/ActorHub.cs)) reads
only `progression.bonus.{maxHp,atk,defense,arm1,arm2}` and returns `primary` unchanged when all five are
zero.

**Not a defect — a boundary worth writing down.** It is the *only* way a derived channel reaches a Unity
combat field, and it is `decisions.md` *P2* by design. An aptitude that wants to move `hp` moves
`progression.bonus.maxHp`; there is no other door.

> **Verdict: DOCUMENT.** And note the rounding: the merge does `Math.Round` into `long`/`int`. An
> aptitude contribution that is exact in `long` becomes `double` inside `DerivedStatDef` and is rounded
> back — the seam where the overflow standard's `long` rule is temporarily broken by the shipped type.
> §10.7 of the power SSOT decided `double` in stat composition **stands**; this module states where the
> narrowing happens so `aptitude-resolve` sizes coefficients knowing it.

### 3.7 `unitClass: null` × 29, `statClass: null` × 3

Owned by [spec-unit-class-close.md](spec-unit-class-close.md). Listed here only so the register is the
complete picture of the path rather than a partial one.

### 3.8 `status.expose.*` — registered vocabulary, zero readers

*"Registered vocabulary with ZERO readers today"*, per the catalog's own note.

> **Verdict: DOCUMENT — and adopt it as the precedent.** This is the repo's existing, labelled way to
> ship a declared-but-unwired thing honestly. Every item in this register that ends as "document"
> should carry a note in the same shape.

### 3.9 The four empty plugins are asserted by a test that does not notice they are empty

`StatSystemTests.cs:205-207` asserts `ClassStatPlugin` sorts first and before `CheatScaleStatPlugin`.
**The ordering is tested; the emptiness is not.** A green suite therefore says nothing about whether
any of them does anything.

> **Verdict: this is the shape of the whole problem.** Every item above is *declared, registered,
> ordered, documented and inert* — and each has a green test beside it. **A reconcile module exists
> because nothing in CI can tell "wired" from "reserved".** §6 test 6 is the repair.

---

## 4. Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "StatSystem|ActorHub|BattleStat|Composer"
.\scripts\guard-power.ps1
python scripts\audit-magic-numbers.py
python scripts\audit-overflow.py --targets A3
```

---

## 5. Project structure

```text
src/FusionRpg.Core/Stats/Plugins/StubStatPlugins.cs           3.1 - delete or comment
src/FusionRpg.Core/Battle/BattleStatComposer.cs               3.2a - known-channel set ONLY (no logic change)
src/FusionRpg.Injector/CheatState.cs                          3.3 - hydrate PowerIndex into ActorHub
src/FusionRpg.Core/Stats/Derived/Subsystems/RpgProgressionSubsystem.cs   3.5 - the stub curve
docs/architecture/power/ssot-power-scale.md                   3.5 - a §10 row, or a deletion
docs/architecture/stat-system.md                              3.1 - which pipeline reaches which channels
tests/FusionRpg.Core.Tests/Stats/SeamCoverageTests.cs         3.9 - the standing guard
```

**No new type, no new mechanic, no new balance number.** If this module produces one, it has exceeded
its scope.

---

## 6. Testing strategy

| # | Test | Asserts |
|---|---|---|
| 1 | `An_actor_subsystem_reaches_a_composed_channel` | End to end through `ActorHub.ResolveDerived`. The baseline that 3.1 shows does not exist for plugins |
| 2 | `Battle_and_overlay_compose_the_same_channel_from_the_same_input` | **3.2's repair**, via `ChannelMods` on the battle side and a subsystem on the overlay side. The test `aptitude-resolve` depends on, moved here where it can be made to pass |
| 2b | `Every_aptitude_edge_channel_is_in_the_battle_known_channel_set` | **3.2a**, as a set difference. The deliverable that keeps working as families are added — which is how the set drifted in the first place |
| 3 | `Theta_is_non_zero_on_both_paths_when_hydrated` | 3.3 + 3.4 |
| 4 | `Magnitude_is_flat_when_theta_is_zero` | The *symptom*, pinned deliberately — so the failure mode is recognisable rather than mistaken for a coefficient bug |
| 5 | `Progression_bonus_stub_curve_is_gone_or_inventoried` | 3.5. Passes either way; fails on the current state, which is neither |
| 6 | `Every_registered_plugin_and_subsystem_either_contributes_or_declares_itself_inert` | **3.9.** The standing guard: an empty `Contribute` needs an explicit `[DeliberatelyInert]`-style declaration, so reserved and forgotten stop looking alike |
| 7 | `Derived_to_primary_bridge_is_five_channels` | 3.6, as a canary — widening it silently is how a sixth door appears |

**Test 6 is the one that outlives the module.** Everything else fixes a known item; test 6 is what
stops the next reserved seam sitting inert for two years with a green suite beside it.

---

## 7. Boundaries

**Always** — give every item a verdict; keep verdicts to fill / wire / replace / delete / document;
land the `§10` row in the same change as the curve it describes.

**Ask first**

- **Deleting `ClassStatPlugin`.** It is registered and order-tested, so removal touches a shipped test
  — cheap, but it is someone else's stub to lose.
- Touching `AchievementStatPlugin` / `ItemStatPlugin` / `BuffStatPlugin` (§3.1) — three other programs.
- Anything that moves a battle golden.

**Never**

- Add a mechanic. This module removes disagreement only.
- Add a fourth composition path, or unify the two composers (§3.2).
- Change `BattleStatComposer`'s compose logic. The known-channel set is the only line this module
  touches there, and T5's byte-identical gate is why.
- Leave an item without a verdict. *"Look at it later"* is what produced this register.
- Delete a stub without checking who reserved it and telling them.

---

## 8. Success criteria

1. Every one of the nine items has a landed verdict.
2. An aptitude-shaped contribution reaches a composed value on **both** paths — subsystem on the
   overlay, `ChannelMods` on battle — asserted, with `BattleStatComposer`'s logic unchanged.
3. `Θ` is hydrated and non-zero on both paths; the zero-`Θ` symptom is pinned by a test.
4. `progression.bonus.*`'s level curve is replaced **or** inventoried in §10 — not neither.
5. Test 6 exists and is green: no registered contributor is silently empty.
6. **Zero goldens move**, or a move is attributed to exactly one item and joins the combined re-bless.
7. [spec-aptitude-resolve.md](spec-aptitude-resolve.md) §2 is corrected to name `IActorStatSubsystem`,
   and §2a to name `ChannelMods` rather than a subsystem seam on the battle side.
8. **`BattleStatComposer` has no logic change** — only `BuildKnownChannels` widens (§3.2a).

---

## 9. Open

**9.1 ~~Whether `ClassStatPlugin` should be filled~~ — CLOSED 2026-08-26.** Keep it, comment it, do not
fill it yet. §3.1 has the reasoning and the named future case (`attackInterval` as `Agility`'s
overlay-side speed route). **When that case arrives it is a scoped change to one empty method**, which
is why the seam is worth keeping rather than rebuilding.

**9.2 ~~How much of 3.2 belongs to the battle stream~~ — CLOSED 2026-08-26.** Asked by the owner, and
answered by reading [battle-timeline-map.md](../battle-timeline-map.md) and
[battle/spec-readiness-model.md](../battle/spec-readiness-model.md): **none of it.** The battle stream
does not have stat composition in scope, hit this same divergence at T3 and chose to keep the composers
separate, and T5 is a byte-identical freezer that a mover would invalidate. This module uses the
shipped `ChannelMods` seam and touches `BattleStatComposer` only to widen a set. See §3.2.

**9.3 Sequencing against T5 — DECIDED 2026-08-26: land before T5 opens.** The battle map records T5 as
specced and **nothing built**, so the window is not open. Class-system moves the goldens now, they
settle, and T5 later proves byte-identity against a stable baseline — which is what a freezer needs.
The reasoning it rests on: Adding a `ChannelMods` producer
moves nothing while **nobody has an allocation** — zero points, zero mods, zero delta — so
`aptitude-resolve` lands byte-identically (its success criterion 9). **The golden move arrives with
`point-economy`**, when an actor first holds points. Per *Golden ordering across streams*, that lands
either well before T5 opens or after its gate passes — **never inside its window**, or T5 cannot
attribute its own hashes.

---

## 10. Design-gate checklist

```
[x] Subsystems identified: stats (primary + derived), battle, power scale, tunables, caps.
[x] Read this session: DESIGN-GATE.md, decisions.md (Stats, Stat compose, Stat extension, Actor Hub
    SSOT, P1, P2, Power scale, Combat resolution SSOT, Golden ordering across streams, Battle time
    model, Magic numbers rows), stat-system.md (FULL), actor-hub-ssot.md §7 + §8 ban list,
    ssot-power-scale.md §4.6/§10/§11, tunables-ssot.md, derived-stats-map.md,
    battle-timeline-map.md (FULL - the DESIGN-GATE Battle row), battle/spec-readiness-model.md
    (the stat-channel and registration sections). The last two are what closed §9.2.
[x] Every factual claim cites file:line.
[x] Verified against CODE, not comments - and this module exists BECAUSE comments and code
    disagreed. Read this session: StubStatPlugins.cs (all four), StatSystemBootstrap.cs:17-23,
    StatSystemTests.cs:205-207, IStatModifierPlugin.cs + ModifierPluginRegistry, ModifierBag.cs:3-8,
    StatModifier.cs:3-17, StatContext.cs, IActorStatSubsystem.cs:5-10, ActorHub.cs:1-110,
    RpgProgressionSubsystem.cs (full), DerivedComposer.cs:13-36, BattleStatComposer.cs:46-61
    (BuildKnownChannels) and :88-145, CheatState.cs:25-40, Server/Program.cs:101, StarPolicy.cs:6,
    ExpeditionResolver.cs:246-250, WebMatchService.cs:281 - the four shipped ChannelMods producers
    that §3.2 makes aptitudes the fifth of.
[x] Read the surrounding section of every rule quoted - actor-hub-ssot §7 and its ban list together;
    P2's row in full, including the "until dedicated bonus ADR" clause that makes §3.5 this
    program's business.
[x] Constraints TESTED, not assumed. The claims that ClassStatPlugin IS registered, that
    BattleStatComposer references no subsystem, that no host passes a level delegate, and that
    §10 contains no progression.bonus row were each verified by running a grep or reading the file,
    not inferred. §3.5 is stated as LATENT because the early-return was read, not because it felt
    safe to say.
[x] Nothing contradicts a §2 invariant. Invariant 4 (single writer) is untouched - §3.6 documents
    the one legal derived -> primary door rather than adding another. Invariant 14 (one power
    ladder) is what §3.5 enforces.
[x] Corrections propagated - §2 corrects spec-aptitude-resolve.md §2, which is edited in the same
    pass, and the map gains this module plus a corrected §2a.0.
```

---

## 11. Related

- [spec-aptitude-resolve.md](spec-aptitude-resolve.md) — the module this unblocks, and the spec §2 corrects
- [stat-system.md](../stat-system.md) · [actor-hub-ssot.md](../actor-hub-ssot.md) §7 — the two pipelines
- [power/ssot-power-scale.md](../power/ssot-power-scale.md) §10 — the closed inventory §3.5 must join or clear
- [derived-stats/spec-unbuilt-reconcile.md](../derived-stats/spec-unbuilt-reconcile.md) — the reconcile precedent, one layer up
- [decisions.md](../decisions.md) — *P1*, *P2*, *Actor Hub SSOT*, *Combat resolution SSOT*
