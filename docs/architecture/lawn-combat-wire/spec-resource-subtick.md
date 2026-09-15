# Spec: `resource-subtick`

**Program:** `lawn-combat-wire` · **Map:** [../lawn-combat-wire-map.md](../lawn-combat-wire-map.md)
**Depends on:** — (leaf, parallelisable)
**Implements:** the repo's own named follow-up **S10.1**

---

## Objective

Resource regeneration cannot currently be expressed at a useful rate, so it is switched off
everywhere — which means no action can carry a resource cost without permanently draining its actor.
This blocks `basic-attack-cost` in this program, and it blocks battle's own basic attack for the same
reason.

The cause is rounding, and the repo already diagnosed it and named the fix.
`data/tuning/battle-resources.v1.json` `_meta.regenIsAbsentOnPurpose`:

> *"`ResourceChannelReader.RegenPerTick` rounds the channel to a whole long, and a battle round runs
> several hundred ticks … the SMALLEST expressible non-zero rate accrues ~300 poise per round against
> a spend of 100 … **A sub-tick unit is a named follow-up (spec S10.1)** and regen earns its rows
> then."*

So `BattleRuleset.BaseResourceRegen => 0` (`BattleModels.cs:412`) is **not a design position against
regen** — it is the only safe value while the unit is too coarse.

Success: a regen rate finer than one whole unit per tick is expressible and accrues exactly, with no
drift, deterministically — after which regen earns its tuning rows in **both** battle and the lawn.

## Tech stack

`FusionRpg.Core` — `ResourceChannelReader`, `ActorResourcePools`. Unity-free.

## Commands

```powershell
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Resource"
dotnet test tests/FusionRpg.Core.Tests --filter "Category=BalanceGuard"
```

## Project structure

| Path | Duty |
|---|---|
| `src/FusionRpg.Core/Stats/Derived/ResourceChannelReader.cs` | Expose regen in per-mille-per-tick instead of a rounded whole |
| `src/FusionRpg.Core/Actions/Cost/ActorResourcePools.cs` | Carry a `long` remainder per pool; emit whole units as they accrue |
| `data/tuning/battle-resources.v1.json` | Gains its regen rows once the unit exists (a later, separate balance act — not this module) |

## Code style

Accumulate in per-mille, carry the remainder, divide exactly once:

```csharp
// per pool, per tick — integer per-mille carry, no drift
acc += regenPerMilleTick;          // long
long whole = acc / 1000L;          // divide LAST, exactly once
acc -= whole * 1000L;              // carry the remainder forward
if (whole > 0) pool.Add(whole);
```

This is the **same discipline `KernelDriveHost` already applies to time** — it re-arms off the event's
own `DueTick` rather than "now", *"so a stuttering frame would permanently slow DoT cadence instead of
merely delaying it"* (`KernelDriveHost.cs:186-192`). That is carry-correction for *when*; this is
carry-correction for *how much*. Cite that precedent rather than inventing a rationale.

## Testing strategy

| Level | Cases |
|---|---|
| Core unit | A rate below one unit per tick accrues correctly over N ticks (e.g. 300‰/tick yields exactly 3 units in 10 ticks, not 0 and not 10) |
| Core unit | **No drift**: over 10,000 ticks the accrued total equals `floor(rate × ticks / 1000)` exactly |
| Core unit | Determinism: same sequence every run and platform |
| Core unit | A zero rate accrues nothing and never touches the pool |
| Core unit | The remainder survives a pool read; reading does not silently reset the carry |
| Core unit | Accrual never exceeds `resource.max.*` — overflow into a capped pool discards the excess *and the carry*, so a full pool does not bank a windfall |
| Regression | **Battle stays byte-identical** while regen rows remain absent — this module changes representation and adds capability, never live behaviour |

## Boundaries

- **Always:** in integer per-mille accrual, divide by 1000 last, exactly once; carry the remainder.
- **Always:** leave `BaseResourceRegen` returning 0 in this module. Making regen *expressible* and
  *choosing its values* are different acts; the second is a balance pass.
- **Ask first:** changing the per-mille unit to something finer. 1/1000 is chosen to match the repo's
  existing per-mille convention; a different denominator fragments it.
- **Never:** drifting accumulation; per-tick rounding; a second regen path beside `ActorResourcePools`. *("float accumulation" ban removed 2026-09-15 per owner ruling: floating-point allowed)*

## Success criteria

- [ ] A sub-whole-unit regen rate accrues exactly over time, proven by a drift test over ≥10,000 ticks.
- [ ] Accrual is exact with no drift. *(reworded 2026-09-15 per owner ruling: floating-point allowed)*
- [ ] Battle behaviour **byte-identical** with regen rows still absent.
- [ ] `BaseResourceRegen` still returns 0 — this module unblocks the rows, it does not author them.
- [ ] The capped-pool case discards carry as well as overflow, with a test.
