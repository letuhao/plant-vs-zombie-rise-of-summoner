# Spec: `species-respec`

Module 7 in the [species-build capability map](../species-build-map.md). **Depends on
`demon-type-allocation` (5).**

## Objective

Price changing a species' build. **Owner decision 15: the price rises with the respec count on that
species and decays over time** — it prices **churn**, not investment.

This replaced decision 9 (a level-scaled price) after audit finding **A2** showed that formula named a
quantity that does not exist: souls are player-scoped while species level is per-species, and soul
income is **flat today** anyway, because every live earn passes the Θ pin (`RpgStore.Souls.cs:29`) so
`contentScale = 1.000`. There was no growth curve to take a fraction of.

**Why churn is the right axis, from the lock's own reasoning.** `class-system-ideal.md` §7b.5: free
respec means *"there is no build, only a lookup table keyed on the opponent, and every arrow of the
cycle becomes a menu option."* The behaviour to discourage is **switching**, and a churn-priced respec
targets exactly it. A level-scaled price would instead have punished the player who invested most.

**Checked against the lock, which bans three things:** not a **cooldown** — a cooldown *forbids*, this
only prices, and the decay means being away makes it **cheaper**, which is the precise failure
(*"punishes being away"*) the lock rules out. Not a **cap** — PS-8 holds; you may always respec. Not
**free** — the escalation is real.

## Design

### What costs, and what does not

| Action | Price |
|---|---|
| First override on a species (the player expressing a build for the first time) | **Free** |
| Reverting to the shipped baseline (deleting the override) | **Free** |
| Changing an existing override | **Priced** — this is a respec |

Free-first-override matters: the player is not taxed for having an opinion, only for repeatedly
changing it. It does not reopen the lookup-table problem, because the *second* change already costs.
Free revert matters because returning to the shipped plan is not a build decision.

### The price

```
price(count) = basePrice + basePrice × count × escalationPermille / 1000
```

Linear, not geometric. **Geometric escalation against a flat faucet is how a price becomes a ceiling**,
and soul income is flat today — so linear is the default and geometric is not offered. Widen before
multiplying; divide by 1000 last, exactly once; `long`; overflow throws.

### The decay

`count` decreases by one per `decayDays` elapsed since that species' last respec, **day-quantised in
UTC**, following `ContractPolicy.ElapsedDays`'s own established convention (a minute past midnight is a
new day; a future stamp bills nothing). Decay is applied on read, so no timer and no background job.

`count` floors at zero — a **bounded counter, not a magnitude**, and the comment says so, which is what
PS-8 requires of an exemption.

### State

A small dedicated table. The allocation table is keyed `(scope, scope_key, aptitude_id)` and respec
state is per-species, not per-aptitude, so it does not fit there.

```
rpg_species_respec(player_id, species_id, count, last_respec_utc)   PK (player_id, species_id)
```

### ⛔ The spend path — audit finding A4

**`TrySpendSouls` has zero production callers.** Every shipped sink appends to the ledger directly:
summoning (`RpgStore.Summons.cs:91`), fusion (`RpgStore.Fusion.cs:380`), contract upkeep and slots
(`RpgStore.Contracts.cs:160,267,320,437`), patron (`RpgStore.Patron.cs:55`). **Follow the shipped path**,
not the unused API, or this module becomes the only caller of a seam nothing else exercises.

The spend, the counter increment and the override write happen in **one store transaction**. A crash
between them would otherwise charge a player for a build they did not get, or hand out a build for free.

Its own reason id and its own feature endpoint — `spec-soul-economy.md` is explicit that spends are
never a generic endpoint: *"only feature endpoints spend, each with its own reason."* Idempotent on a
correlation id: a replayed successful spend returns the original result **without spending again**; a
refusal writes no state.

Insufficient balance refuses with the established `409 souls.insufficient`.

### Tunables

`data/tuning/species-build.v1.json` (shared with module 4): `respecBasePrice`,
`respecEscalationPermille`, `respecDecayDays`. A missing key is a **load rejection naming it**.

`RespecPolicy.PriceOf` gains a count argument (`RespecPolicy.cs:32`) — **never a level**. Its
`RespecResource` enum gains `Soul` (decision 1); the existing `Hunger` value was an admitted placeholder
its own doc comment marked *"Ask first"*, and that question is now answered.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter Respec
dotnet test tests\FusionRpg.Data.Tests --filter Respec
dotnet test tests\FusionRpg.Server.Tests
.\scripts\guard-dal.ps1
python scripts\audit-magic-numbers.py --targets M1
```

## Project structure

```
src/FusionRpg.Core/Stats/Aptitudes/RespecPolicy.cs        count argument, Soul resource
src/FusionRpg.Data/Sqlite/RpgStore.SpeciesRespec.cs       new partial slice: counter, decay, spend
src/FusionRpg.Server/SpeciesBuildEndpoints.cs             the feature endpoint
data/tuning/species-build.v1.json                         three respec keys
tests/FusionRpg.Core.Tests/Stats/Aptitudes/RespecPolicyTests.cs   extended
tests/FusionRpg.Data.Tests/SpeciesRespecTests.cs                  new
```

## Code style

- A `partial class RpgStore` slice sharing the one connection/lock/`EnsureHotSchema`/`Reset()` pipeline —
  the correction `spec-point-economy.md:126-130` already recorded for this exact mistake.
- `long` price, `checked`, divide by 1000 last.
- `count` carries a comment naming it a **bounded counter**, exempt from PS-8 by nature.
- `RespecPolicy` holds no bare literal — it is a Policy file and therefore the balance surface.

## Testing strategy

1. **Free cases:** the first override costs nothing; reverting to baseline costs nothing. Both asserted
   directly, because both are easy to regress into "everything costs".
2. **Escalation:** the second, third and fourth changes cost strictly more, and the arithmetic matches
   the formula at named counts.
3. **Decay:** after `decayDays`, the count drops by one and the price falls; after enough days it
   returns to base. Day-quantisation asserted (a minute past midnight is a new day).
4. **Atomicity:** a simulated failure between spend and override leaves **neither** applied.
5. **Idempotence:** a replayed correlation id returns the original result and does not spend twice; a
   refusal writes no state and can be retried.
6. **Insufficient balance:** refused with `souls.insufficient`, no counter increment.
7. **Never a cap:** an arbitrarily high count still permits a respec — it costs more, it is never
   refused for being a respec (PS-8).
8. **Ledger path:** the spend appears in `rpg_soul_ledger` through the same path the shipped sinks use.

## Boundaries

- **Always:** one transaction for spend + counter + override; the shipped ledger path; free revert;
  every number a tunable.
- **Ask first:** the three tunable values (a balance pass owns them); geometric escalation; charging for
  the first override; charging for a revert.
- **Never:** refuse a respec (PS-8); use `TrySpendSouls` without wiring it deliberately; add a cooldown;
  make the price read species level; allow a negative balance.

## Success criteria

1. First override and revert are free; subsequent changes escalate and then decay — all proven by test.
2. Spend, counter and override are atomic; a replay never double-spends.
3. A respec is never refused for being a respec.
4. `guard-dal` green; `audit-magic-numbers` finds no bare literal in `RespecPolicy`.
