# Spec: loam-legions (wave 3)

**Status:** **Sealed 2026-08-23** — owner-approved (all open items resolved below). Module id
`loam-legions` in the [loam capability map](../loam-map.md). Depends on `loam-turn` (shipped, gate
passed). **Design source:** [empire-economy-ideal.md](../empire-economy-ideal.md) §7.4–§7.7, §10.4,
G1–G2 · [empire-economy-ssot.md](../empire-economy-ssot.md) §6.

This is the first post-gate module. The owner authorized building all five post-gate modules
(2026-08-23, "spec and build them"), and authorized closing this module's remaining open items in the
same pass ("add spec for missing/gap/open items... so we will clear every missing"). Every call this
draft originally left open now has a decision and a reason in **Resolved (2026-08-23)** below.

## Objective

Give a legion its own loam economy: it carries a stock, spends it while beyond the reach of its
faction's territory, and the countdown that creates **replaces** the existing wound-based attrition
mechanic rather than adding a second one beside it. A dedicated **bearer** role trades a member's
combat weight for carrying capacity, so range becomes a real army-composition decision instead of an
automatic side effect of marching.

Success looks like: a legion beyond supply has a legible number of turns before it is lost, not a slow
wound counter nobody watches; a bearer-heavy legion visibly reaches farther than a bearer-light one at
the same headcount; and the existing 100-turn AI-survival property (`AbandonRuleTests`,
`TwoHearthsCampaignTests`) still holds with the new mechanic wired in, not just the old one removed.

## What this replaces, and why removing it is in scope

`SupplyGraph.cs`'s `AttritionWoundMilli = 50` gives an out-of-supply force roughly twenty turns of
wound accumulation before its members are gone one by one. Ideal §7.5 calls this **too slow to change
any decision** — by the time it bites, the player has usually already resolved the situation some other
way, so the number is decorative. A 4–8 turn leash the player can count is the actual replacement, per
the ideal's own framing: *"we ran out and the dark took them"* is a better story than a wound counter.

This is a **removal**, not an addition: `SupplyGraph.Starve` and the wound-accumulation path go away
entirely once carried loam covers the same job. `SupplyGraph.Recover` (healing while in supply and
holding) is untouched — it is the game's only healing mechanic and has nothing to do with attrition.

**Four existing tests assert the mechanic being removed**
(`tests/FusionRpg.Core.Tests/World/SupplyTests.cs`):
`A_legion_out_of_supply_takes_attrition_once_a_turn`, `A_legion_standing_in_supply_takes_none`,
`Attrition_eventually_finishes_a_stranded_legion`, `A_faction_with_no_seat_of_its_own_has_no_supply_
and_never_starves`. The first and third assert exact wound math that no longer applies once burn
replaces it; the second and fourth assert properties (`in supply → nothing bad happens`,
`no-seat faction is exempt`) that still hold under the new mechanic and should be rewritten against it
rather than deleted, per this program's own G-C precedent (an exemption gets re-proven at every place
its logic moves, not assumed to survive a refactor untested).

## Design

### New state

- `WorldEntityMember.Role` — `Fighter` (default) or `Bearer`. A genuinely new field on a record with no
  collision (confirmed clean against the shipped `WorldEntityMember`).
- `WorldEntity.CarriedLoam` (`long`) — an entity-level pool, not per-member. Members carry as a crew;
  splitting a legion is already a separate, unmodelled feature and this does not need to anticipate it.

### Capacity and burn — the leash, and the degeneracy SSOT §6 already named

> Capacity scaling with *every* member is degenerate: if capacity and burn both scale with headcount,
> range = capacity/burn is constant and the logistics layer evaporates.

So the two terms must scale with **different** things, or bearers buy nothing:

- **Capacity** scales with **bearer count only**: `Capacity = BearerCount × CarryPerBearer`. A legion
  with zero bearers has zero capacity — it can march inside supply forever (free top-up, below) but has
  no reserve at all the moment it steps outside it.
- **Burn** scales with **total headcount** (mouths to feed, fighters and bearers alike):
  `Burn = MemberCount × BurnPerMember`, charged once per turn the legion is **not** inside
  `SupplyGraph.ConnectedSectors` for its own faction (that method is already a clean, uncached BFS with
  no other production caller — confirmed, safe to add a second one).
- **Leash length** = `Capacity / Burn` turns, which is the number the ideal wants to be legible and
  plannable (§7.5 argues 4–8 turns as a target, not a rule — this needs the same L9-style harness
  treatment `loam-calc`'s constants got, not a guess baked in here).

**Resolved: found by a harness, not guessed here.** `LoamPolicy`'s own docstring already establishes
the method this program uses for every number of this kind — "L9's harness measures them... choosing
them earlier is guessing with extra steps" (`tasks/loam-plan.md`'s own "Open" section says the same).
The task list below schedules a `LegionSupplyEconomyTests` harness, mirroring L9, that tunes
`CarryPerBearer` and `BurnPerMember` against the 4–8 turn leash target before either constant ships —
this is the resolution, not a deferral of one.

### Free top-up in supply, spend beyond it

- **Inside** `SupplyGraph.ConnectedSectors`: at the top of `Pressure` (before the burn check), a legion
  tops its `CarriedLoam` up toward its `Capacity`, drawn from its faction's local `TerritoryComponents`
  pool at the sector it currently occupies — the same pooled-stock mechanic sector upkeep already draws
  from, not a second ledger.
- **Beyond** it: no top-up: the legion burns from whatever it is carrying. When `CarriedLoam` would go
  below zero to cover the burn, the legion is destroyed outright (see **Resolved**, below).

**Draw order, resolved: sector upkeep first, legion top-up second, and a short pool never burns a
legion for it.** `LoamPhases.Pressure` draws the component's sector upkeep from the pool exactly as it
does today; only what remains funds legion top-ups that turn, split proportionally across every legion
present the same way sector shares already are (`DrawProportionally`'s own shape, reused, not a second
draw rule invented beside it). If nothing is left, a legion simply does not top up this turn — that is
never itself a burn or a step toward destruction, only a missed refill. Reasoning: ideal §7.4 frames
*hold* and *project* as competing sinks on one pool, and between them **ground is the foundation** —
losing a sector costs every legion in the component its supply status the following turn, so protecting
the ground protects the armies indirectly. Legions never get punished twice for the same shortfall (once
via their sector fading, again via a missed top-up starving them) because a missed top-up is free.

### What happens at zero — destroyed outright, not a wound path

**Resolved: destroyed outright**, the entity removed from `world.Entities` the turn its `CarriedLoam`
would go negative covering burn — the same disappearance shape a fully-attritted legion already has
today, arrived at by starvation instead of by wounds. Reasoning: the ideal's own phrase — *"the dark
took them"* — reads as final, not gradual, and the leash is already the whole tension (§7.6: "the gap
between not-anchored and gone is where the player reacts"); stacking a second, wound-based countdown
after the leash expires would be two countdowns for one warning, the same "two multipliers is one too
many" shape A3 already flagged and closed elsewhere in this program (map §A3). A legion that runs its
leash out gets no second chance inside this module — the reaction window is the leash itself, and it
is turns long, not one final roll.

### G1's bootstrap spend, scoped to what exists today

Ideal G1 resolves the "first rootworks needs loam that does not exist yet" paradox with *"a legion may
spend carried loam to hold the ground it stands on."* Structures do not exist until wave 4–5
(`structure-substrate`, `loam-structures`), so within `loam-legions`' own scope this cannot yet mean
"fund construction." What it **can** mean now, without waiting on structures: a legion standing on a
sector its own faction holds may spend carried loam to add directly to that sector's own stability
recovery this turn, on top of (or instead of) the component-pool mechanism `LoamPhases.Pressure`
already runs — the same shape G1 wants (an army's own reserves keeping newly-taken ground alive), built
against what is actually shipped rather than against structures that are two waves away.

**Resolved: a new explicit order, `WorldCommandKinds.Sustain`, at 1:1.** A player-issued command (not
automatic — G1's own framing is "a legion **may** spend," an active choice, matching how `Clear`/`Claim`
are already explicit orders rather than implicit stance effects) naming the legion and an amount of
`CarriedLoam` to spend, valid only while the legion stands on a sector its own faction holds. The spend
is converted **1:1** into a positive contribution to that sector's `FadePolicy` balance for the current
turn — no new formula, the same `Apply(currentStabilityMilli, balance)` shape already proven, just an
additional positive `balance` term contributed before the component's automatic accounting runs. 1:1
because there is no existing exchange-rate precedent anywhere in this program to derive a different
ratio from, and a legible 1-for-1 spend is easier for a player to reason about at the exact moment G1
describes — *"your army burns its own reserves keeping the ground alive"* — than a rate that needs a
tooltip to explain.

### Phase placement — two mechanisms, two different timings, both inside `Pressure`

An adversarial audit caught this spec originally saying two contradictory things: that `Sustain`'s
balance contribution lands "before the component's automatic accounting runs," while also saying the
whole `LegionSupply` pass — which a first draft conflated `Sustain` with — runs *after*
`LoamPhases.Pressure`. They cannot both be true, and they do not need to be, because `Sustain` and the
burn/top-up pass are different mechanisms with different timing requirements:

1. **`Sustain` resolves first, at the top of `Pressure`, before `LoamPhases.Pressure`'s own
   accounting.** Its whole purpose (G1's tension — a legion's spend can save the very sector that would
   otherwise be picked as weakest this turn) only holds if the automatic weakest-selection sees
   `Sustain`'s contribution already applied. `Sustain` spends the issuing legion's own `CarriedLoam` —
   a resource this pass never draws from the shared component pool — so resolving it first creates no
   ordering conflict with the sector-upkeep-first draw rule below; it only matters for making
   `Sustain`'s effect visible to this same turn's fade decision, which requires it to run first.
2. **`LegionSupply`'s burn/top-up pass runs after `LoamPhases.Pressure`'s sector-upkeep draw** — this is
   the resolved draw order (sector upkeep first, legion top-up second, from the shared pool) and is
   unaffected by point 1, since `Sustain` never touches that shared pool at all.

Both live inside `Pressure`, replacing `SupplyGraph.Run`'s attrition call between them; `SupplyGraph.
Recover` stays exactly where it is, running before both (healing is not part of this economy).

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~Supply
dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~Loam
dotnet test tests\FusionRpg.Core.Tests
dotnet test tests\FusionRpg.Data.Tests
dotnet test tests\FusionRpg.E2E.Tests --filter FullyQualifiedName~World
dotnet test tests\FusionRpg.Guard.Tests
```

## Project structure (proposed)

```
src/FusionRpg.Core/World/WorldState.cs                  → WorldEntityMember.Role, WorldEntity.CarriedLoam
src/FusionRpg.Core/World/WorldCanonical.cs               → both fields into the hash — part of the batched post-gate golden move, see spec-loam-texture.md
src/FusionRpg.Core/World/Loam/LoamPolicy.cs              → CarryPerBearer, BurnPerMember (harness-tuned)
src/FusionRpg.Core/World/Loam/LegionSupply.cs (new)      → top-up, burn, the leash — same shape as LoamPhases
src/FusionRpg.Core/World/Movement/SupplyGraph.cs         → Starve/AttritionWoundMilli removed; Recover kept
src/FusionRpg.Core/World/Turn/TurnEngine.cs              → Pressure: Sustain resolves first, then LoamPhases.Pressure, then LegionSupply's burn/top-up
src/FusionRpg.Core/World/Movement/SustainResolver.cs (new) → the Sustain command
tests/FusionRpg.Core.Tests/World/SupplyTests.cs          → attrition tests rewritten against the new mechanic
tests/FusionRpg.Core.Tests/World/Loam/LegionSupplyTests.cs (new)
docs/architecture/decisions.md                           → the batched golden-move row (spec-loam-texture.md owns this note)
```

## Code style

Same discipline as the rest of this program: `long` for anything that accumulates, integer math,
multiply-before-divide, one canonical helper reused rather than a second copy (`SupplyGraph.
ConnectedSectors` gets a new caller, not a new BFS).

## Testing strategy

- **The leash is legible**: a legion beyond supply with a known capacity and burn survives exactly
  `Capacity / Burn` turns, not one more or fewer.
- **Bearers change the leash, headcount alone does not**: two legions of equal size, one all fighters
  and one with bearers, have different ranges; two legions of equal bearer count but different total
  size have the *same* capacity but different burn (the degeneracy SSOT §6 warns against, proven absent
  rather than assumed absent).
- **In supply, free and immediate**: a legion inside `ConnectedSectors` tops up toward capacity without
  spending anything of its faction's, and never burns.
- **The two rewritten `SupplyTests.cs` properties**: in-supply takes no burn; a faction with no seat is
  exempt (mirrors G-C, needs its own explicit test the way G-C's exemption needed re-proving at every
  site it touched this program, per the L20 lesson).
- **The hundred-turn and sixty-turn survival properties still hold**: `AbandonRuleTests`'s existing
  100-turn Zomboss survival test and `TwoHearthsCampaignTests`'s 60-turn combined campaign must both
  stay green with the new mechanic wired in — regression, not just new coverage.

## Boundaries

- **Always:** `long` accumulation with no silent overflow; the leash reproducible to the turn; the two
  existing long-run survival tests kept green, not deleted for convenience.
- **Ask first:** changing any of the four resolved calls below once implementation starts (destruction
  vs. wounds, the draw order, the `Sustain` command's shape/rate, the harness-vs-guess methodology for
  the two constants) — resolved here does not mean unreviewable, only that a change now needs a stated
  reason, the same as any other locked call in this program.
- **Never:** a second attrition mechanic running beside this one; capacity or burn scaling with the
  same headcount term (the exact degeneracy already named and to be avoided by construction).

**Confirmed by audit, not assumed**: no shipped code ever merges two `WorldEntity` records or splits
one into two (grepped across `World/`, including `BattleApplication.cs`) — entity-level `CarriedLoam`
is safe on that basis today. This spec already named the deferral ("splitting a legion is already a
separate, unmodelled feature"); nothing currently guards the assumption in CI, so a future feature that
adds merge/split must also decide what happens to `CarriedLoam` — noted here so that feature's own spec
finds this one rather than discovering the gap fresh.

## Success criteria

1. `SupplyGraph.Starve`/`AttritionWoundMilli` removed; `Recover` unchanged.
2. Leash length is exact and reproducible; bearer count changes it, plain headcount does not.
3. `AbandonRuleTests`'s 100-turn and `TwoHearthsCampaignTests`'s 60-turn properties both still pass.
4. The two new hashed fields land in the **one** batched golden move across all five post-gate specs
   (`spec-loam-texture.md` owns this decision, added after an adversarial audit caught each spec
   independently reopening a budget `tasks/loam-plan.md` had explicitly closed at two) — not a separate
   move of its own.
5. `CarryPerBearer`/`BurnPerMember` are tuned by `LegionSupplyEconomyTests` against a 4–8 turn leash,
   not hand-picked.
6. All four guard scripts green.

## Resolved (2026-08-23)

Every item this spec's own first draft left open, decided in this pass, per the owner's authorization
to "clear every missing" rather than leave them for a second round:

- **Destruction outright**, not a wound-based starved state, when carried loam runs out beyond supply.
- **Draw order**: sector upkeep first, legion top-up second, from the same component pool; a short
  pool never itself burns a legion, it only skips that turn's top-up.
- **`Sustain`**: a new, explicit, player-issued `WorldCommandKind` spending carried loam 1:1 into a
  sector's own `FadePolicy` balance for the current turn — G1's bootstrap spend, scoped to what is
  buildable before structures exist.
- **`CarryPerBearer`/`BurnPerMember`**: not guessed here — scheduled as a harness task
  (`LegionSupplyEconomyTests`), following this program's own L9 precedent, tuned against the 4–8 turn
  leash the ideal names as its target.

**Resolved after an adversarial audit (2026-08-23)**, which found this spec's first pass internally
contradicted itself on when `Sustain` actually resolves, and reopened a golden-move budget the plan had
already closed:

- **`Sustain` resolves before `LoamPhases.Pressure`'s accounting; `LegionSupply`'s burn/top-up resolves
  after it.** Two mechanisms, two timings, both correct for their own purpose — the earlier draft's
  Design section and its Phase Placement section disagreed about this because a first pass treated
  `Sustain` as part of the same pass as burn/top-up; they are not.
- **One batched golden move across all five post-gate specs**, not one per spec — see
  `spec-loam-texture.md`'s cross-spec note, which now owns this decision.
