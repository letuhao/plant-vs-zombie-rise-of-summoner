# Spec: species-skills

Module id `species-skills` in the [combat unification map](../combat-unification-map.md). Depends on
`battle-adoption` (shipped) and the battle kernel through T9 (shipped 2026-08-28).
**Written 2026-09-04.** **This spec replaces Wave S of
[spec-battle-enrichment.md](spec-battle-enrichment.md)**, which was drafted 2026-08-21 against a round
loop that no longer exists and was never rebased.

## Objective

Give `skill.cooldown.{category}` and `skill.effectiveness.{category}` their first readers, so an
actor's aptitudes actually change how often its actions come up and how hard they land.

These are registered derived channels with **zero callers**. The registry says so in its own words:

> *"No reader: `CooldownMath.ApplyReduction` and `ActionEnvelope.CooldownChannel` both exist with zero
> callers — the action/timeline layer that would wire them is unbuilt."* — `DerivedStatRegistry.cs:179`

That sentence was true when written. It is now **stale in its diagnosis**: the action/timeline layer
*is* built. What is missing is two reads, and that is all this module is.

## The rebase — what changed since Wave S was written

Wave S proposed a `SkillDef` record (id, cooldown in **rounds**, action kind, targeting policy) and a
code-first `SkillCatalog`. Every one of those five pieces now exists under a different, shipped name.
**Building Wave S as drafted would create a fifth content system** — the exact defect
[DESIGN-GATE.md](../../DESIGN-GATE.md)'s action row names: *"Inventing a third vocabulary is the exact
defect the atom program exists to stop."*

| Wave S proposed | What shipped instead | Evidence |
|---|---|---|
| `SkillDef.id` | `ActionRow.ActionId` | `RpgStore.Actions.cs:321` |
| `cooldown (rounds)` | `ActionEnvelope.CooldownTicks` + `Class` + `CooldownKey` + `StartsAt`, on **absolute simulation ticks** | `CooldownLedger.cs:15` — *"Cooldowns are absolute ticks on the simulation clock"* |
| `action kind + params` | `ActionKind` (closed, 3) + a container of atoms | `ActionEnums.cs`, `decisions.md:97` |
| `targeting policy` | `ActionTargetSpec` / `ActionTargetResolver` | `Actions/ActionTargetSpec.cs` |
| `SkillCatalog`, code-first | `ActionCatalog`, **wired into battle** by T19 on 2026-08-30 | `BattleRunState.cs:118`; `aura-skill-todo.md:1415` `[x]` |
| "Selection: … Initiative unchanged" | `ReadinessDriver` + `ITurnEconomy` | `Battle/Timeline/` |

**So this module builds no catalog, no `SkillDef`, and no selection policy.** A skill *is* an action
that costs a cooldown. The vocabulary is closed and it already fits.

## Design

### 1. The cooldown read

`ActionEnvelope.CooldownChannel` already names which `skill.cooldown.{category}` channel an action's
cooldown reads. `CooldownMath.ApplyReduction(baseTicks, reductionRatioPm)` already implements the
reduction, with a structural one-tick floor and half-away-from-zero rounding, and is already
covered by `SkillModifiersTests`. Neither has a caller.

**The change:** where a cooldown is armed, resolve `CooldownChannel` against the acting actor's
derived sheet and pass the per-mille through `ApplyReduction` before it reaches the ledger.

- **Arming site, not evaluation site.** The cooldown is computed once when it is set, never
  re-derived while it runs — `CooldownLedger` stores an absolute tick, and its own comment explains
  why that is the point: *"An absolute tick has nothing to go stale."* Reducing at read time would
  make a mid-battle haste change retroactively alter a cooldown already ticking.
- **A null `CooldownChannel` reads nothing** and arms at base ticks. That is the neutral path and it
  must stay allocation-free.

### 2. The effectiveness read

`skill.effectiveness.{category}` is documented in `OverlayCombatCalculator.cs:15` as scaling the
action's output, and **there is no code implementing it** — the two mentions in that file are both
comments. `OverlayCombatCalculator.cs:403` even records where it was meant to sit.

**The change:** apply it as a per-mille multiplier on the resolved payload, in the resolver, on the
same stage the comment names — never as a second multiplier applied by the caller afterwards, which
would put combat math outside the SSOT and trip the parity tests by design.

### 3. Category resolution

Both channels are keyed by category, and `ActionCategory` is a **closed 5-value vocabulary**
(DESIGN-GATE action row). An action's category comes from `ActionRow` (`RpgStore.Actions.cs` reads
`category` at ordinal 33). An action with no category reads the unsuffixed family or nothing —
decided in build, asserted either way, and **never defaulted to a category it does not have**.

### 4. Species → skill mapping

Which species hold which actions is **eligibility**, and `A-E1 eligibility-axis` shipped it
(`content-stack-todo.md:528` `[x]`) — `ActionRow` carries `scope` / `scope_key`, and
`Actions/Eligibility/ActionEligibility.cs` evaluates it. This module **consumes** that; it does not
add a mapping table.

⛔ **Sequencing constraint, and it is real.** The demon species SSOT is mid-regeneration: the id
scheme changed (186 deletions / 289 additions uncommitted under `data/seed/demons/species/`, 14 Core
tests red on renamed anchors), and `demon-corpus-self-heal` still has four open items including two
model reruns. **Authoring species→action eligibility rows against those ids today means redoing them
after.** So this module splits:

| Half | Depends on the species corpus | Ships when |
|---|---|---|
| **The two reads** (§1, §2) | **No** — they read an actor's channels, whatever produced them | Immediately |
| **Authored eligibility content** | **Yes** | After `demon-corpus-self-heal` closes its four |

The first half is what closes the reader gap and un-reds `class-system`'s readiness gate. The second
half is content, and content waits for stable ids.

## The invariant that makes this safe

**A battle in which no actor carries a non-neutral `skill.*` value is byte-identical.** Neutral is
`0‰` reduction and `1000‰` effectiveness, so both reads collapse to the arithmetic identity.

This is the same shape as Wave R's own zero-rider invariant, and it is the acceptance bar: it means
the reads can land **without a `RulesetVersion` bump and without re-blessing a golden**, and any
golden that moves is a defect in the read, not a balance outcome. `RulesetVersion` stays **4**.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Battle"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~SkillModifiers"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Coverage"
.\scripts\guard-single-writer.ps1 ; .\scripts\guard-funnel-delta.ps1
```

## Structure

```
src/FusionRpg.Core/Battle/Timeline/ActionRunner.cs      (arm cooldowns through CooldownMath)
src/FusionRpg.Core/Actions/UsabilityEvaluator.cs        (same reduction where usability is judged)
src/FusionRpg.Core/Combat/OverlayCombatCalculator.cs    (skill.effectiveness multiplier)
src/FusionRpg.Core/Balance/Guards/CoverageReport.cs     (the two families stop being reader-less)
tests/FusionRpg.Core.Tests/Battle/                      (neutral byte-identity, both reads, category)
```

## Testing strategy

1. **The neutral invariant, first and load-bearing:** a full battle with every actor at neutral
   `skill.*` produces a byte-identical report against the current golden. Written before the reads.
2. **Each read proven by contrast**, not by existence: the same battle, same seed, one actor given a
   non-zero `skill.cooldown.{category}` → that actor's action recurs measurably sooner and *nothing
   else in the report moves*. Likewise for effectiveness on damage.
3. **A falsifier per read** — delete the read, the contrast test goes red. A read asserted by nothing
   is worth nothing; this repo has already paid for one green run that ran zero tests.
4. **Category routing**: an action in category A is unaffected by a channel value on category B.
5. **The floor holds**: an absurd reduction cannot produce a zero-tick cooldown
   (`CooldownMath.MinTicksFloor` — already tested at 100 000 000 %, extend to the wired path).
6. **The readiness gate moves**: `CoverageReport` shows both families with readers. This is the
   observable outcome for `class-system`, and it is the module's real receipt.

## Boundaries

- **Always:** resolve the cooldown at arming; keep combat math inside the resolver; keep the neutral
  path allocation-free; use the closed `ActionCategory` vocabulary.
- **Ask first:** any non-neutral default; giving an action a category it does not carry; authoring
  species eligibility before the corpus settles.
- **Never:** a `SkillDef`, a `SkillCatalog`, or any second description of an action; a cooldown in
  rounds; a per-mille magnitude on `int` (`long`, per invariant 13); a clamp where the floor should
  throw; applying effectiveness outside the resolver.

## Success criteria

1. `skill.cooldown.*` and `skill.effectiveness.*` each have a real, falsifier-proven reader.
2. The neutral battle is byte-identical; `RulesetVersion` stays 4; no golden re-blessed.
3. `CoverageReport` no longer lists either family as reader-less. 4. No new content type exists —
   the diff adds reads, not vocabulary. 5. The species-eligibility half is explicitly deferred, in
   writing, with the corpus condition that releases it.
