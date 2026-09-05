# Spec — `actor-channels`

**Program:** `derived-stats` · **Map:** [../derived-stats-map.md](../derived-stats-map.md)
**Depends on:** `catalog-extension` · **Parallel with:** the rest of the band
**Status:** Spec — awaiting review. Not built.

---

## 1. Objective

**Make the six actor resources, reach, and two progression rates into real derived channels.**

**Twenty-one** channels, all **`Pool`** class except the two progression rates (was eighteen when this
spec was written against five resources; `poise` made it six on 2026-08-26 and registration loops
`ResourceIds`, so the code was right and only this table was stale):

| Family | n | Class | Owner spec |
|---|---|---|---|
| `resource.max.{id}` | 6 | `Pool`, magnitude | [resource-hub-ssot.md](../resource-hub-ssot.md) |
| `resource.regen.{id}` | 6 | `Pool`, magnitude | same |
| `resource.efficiency.{id}` | 6 | `Pool`, bounded ratio | same |
| `move.range` | 1 | `Pool`, magnitude | [action-map.md](../action-map.md) |
| `progression.xpRate` | 1 | non-combat, magnitude | §4.1 |
| `progression.breakthroughSuccess` | 1 | non-combat, bounded ratio | §4.2 |

ids: `hp` · `stamina` · `hunger` · `spirit` · `qi` · **`poise`** — one shared set, both factions, no
branch. (`poise` appended 2026-08-26; registration loops `DerivedStatChannels.ResourceIds`, so it was
covered by construction — this line was simply never updated.)

**Unpaired is correct here, not an exemption.** These are `Pool`: precedent is the shipped
`combat.shield.capacity` and `combat.shield.regen`, which never had counterparts. The counters you
would want — drain, root — are **statuses**, which is what `status.expose.*` was reserved for.

---

## 2. The four properties from §3G, restated because they are easy to lose

[actor-hub-ssot.md](../actor-hub-ssot.md) §3G already states these. They are the spec:

1. **Their own family list — never `AllCombatChannelIds`.** That set is asserted at a generated total
   and expands over elements. A resource channel there breaks the assertion *and* gets swept into
   element expansion. **Resources are not element-typed.**
2. **`rpg.*` layer, not `pvz.*`.** Not `StatChannels` entries; they never reach a Unity field. The only
   Writer-backed resource is `hp`. That is the layer split in
   [pvz-middle-layer.md](../pvz-middle-layer.md), not a limitation.
3. **Resource *values* are not derived channels.** Only `max` and `regen` compose. The current value is
   per-actor runtime state resolved lazily as `value + rate × (now − lastTick)` — the same
   compute-on-read law the rest of the server uses. 200 actors × 4 regenerating pools would otherwise
   be **800 recurring scheduled events** against a 0.15 ms kernel slice.
4. **Exhaustion debuffs compose like any other derived mod** — same four compose kinds, same per-channel
   caps, no new ordering rule.

### 2.1 The one thing §3G flags as untested

> *"What is new is that up to **four exhaustion debuffs can stack on one actor at once**, which the cap
> logic has never been tested against."*

**Five** pools exhaust (`stamina` · `hunger` · `spirit` · `qi` · **`poise`**; `hp` depletion is death,
owned by the turn FSM's `Downed` state). All five debuffing simultaneously is reachable in normal play
and **has no test today**. §6 makes it one. ⚠️ This said "four" until 2026-09-05 — `poise` exhaustion
is breaking guard, which is a debuff like the other four, not death (`resource-hub-ssot.md` §1).

### 2.2 `resource.efficiency` is a bounded ratio and needs its §11.6 row

Cost reduction cannot exceed 100% — a negative cost is a faucet, and
[economy-principles.md](../economy-principles.md) is explicit that a faucet without a named sink is
the `+2`/kill incident. **Bounded `0..1` by nature, PS-8 exempt, and the comment must say so.**

`max` and `regen` are **magnitudes** and stay uncapped — they scale on `P(Θ)` like any other.

---

## 3. `move.range`

Reach in cells, `Pool` class. [action-map.md:382](../action-map.md) already promises it here:

> *"Derived channel — cells, distinct from `turn.speed`, which is time. Registers in
> actor-hub-ssot.md §3 with `resource.*`."*

So it lands with this module rather than waiting on the grid — and that matters, because
[action-map.md:573](../action-map.md) is explicit that **range is not retrofittable**, while *"with no
board every range check passes"*, which is what keeps basic-attack adoption byte-identical.

`turn.speed` · `turn.haste` · `turn.moveSpeed` stay **unregistered**. They classify as `Race` under the
taxonomy; the battle stream registers them when it gives them a reader.

---

## 4. Progression — two collisions, both real

### 4.1 `xpRate` must layer, not replace

[actor-hub-ssot.md](../actor-hub-ssot.md) §3B: the kill-XP scale `RpgXpAwardMap.Award.PowerScale`
*"stays XP-only — do not conflate with combat `progression.power`."*

**`progression.xpRate` is a per-actor multiplier layered on top of that award**, never a replacement
and never a second XP curve. Writing a fresh `f(level)` here is the defect the power SSOT exists to
end; this is a *rate*, not a curve, and it reads no level at all.

### 4.2 `breakthroughSuccess` is a roll, and success grants `Θ`

`progression.realm` is pinned at **1.0 permanently** by ADR P1 — a geometric realm multiplier on a
difference-based contest measured `netFactor = 4096`. Realm advancement is **additive in `Θ`**.

> **`breakthroughSuccess` is a probability only.** It changes the *odds* of a breakthrough. What a
> success grants is `Θ`. This channel must never multiply anything, and it must not be read as
> licence to un-pin `realm`.

Bounded ratio, PS-8 exempt, §11.6 row required.

---

## 5. Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Resource|FullyQualifiedName~Exhaustion"
dotnet test tests\FusionRpg.Core.Tests
.\scripts\guard-power.ps1
.\scripts\guard-single-writer.ps1
python scripts\audit-overflow.py
```

---

## 6. Testing strategy

| Test | Asserts |
|---|---|
| `ResourceChannelsNotInCombatRoster` | §2.1 — the failure that rule exists to prevent |
| **`FiveExhaustionDebuffsStack`** | §2.1's untested case: all FIVE pools exhausted at once (incl. `poise`), caps still behave, no ordering surprise |
| `LazyValueMatchesTicked` | `value + rate × elapsed` equals a hypothetically ticked pool at N sample points — proves §2.3's optimisation is not a behaviour change |
| `EfficiencyCannotExceedOne` | A cost never goes negative (§2.2) |
| `MaxAndRegenUncapped` | Magnitudes scale past any literal; **overflow throws, never clamps** |
| `MoveRangePassesWithNoBoard` | Every range check passes with no grid — the byte-identical property |
| `XpRateLayersOnAward` | `xpRate` multiplies `Award.PowerScale`'s output; does not replace it, reads no level |
| `BreakthroughGrantsTheta` | A success adds `Θ`; `progression.realm` still exactly `1.0` |
| `NoGoldensMove` | All 21 at defaults |

`FiveExhaustionDebuffsStack` is the one with a real chance of failing — it is the only assertion here
covering something nobody has ever run.

---

## 7. Boundaries

**Always** — keep resources out of `AllCombatChannelIds`. `rpg.*` layer. Compute values on read.
Comment every bounded ratio with its PS-8 class.

**Ask first** — a **seventh** resource. The six are locked by
[decisions.md](../decisions.md)'s *Resource model* row and a seventh is a product decision. (The sixth,
`poise`, was exactly that decision and it landed 2026-08-26 — this line asked for a gate that had
already been answered.)

**Never** — cap `resource.max` or `resource.regen` (progression ceilings). Branch on faction — labels
are content, never ids. Let a resource channel reach a Unity field (only `hp` is Writer-backed, through
`EntityStatWriter`). Register `turn.*`. Read `progression.realm` as anything but `1.0`.

---

## 8. Success criteria

- [ ] 21 channels live, classified, none in `AllCombatChannelIds`.
- [ ] **Five simultaneous exhaustion debuffs tested** — §3G's named gap closed.
- [ ] Lazy compute-on-read proven equivalent to ticking.
- [ ] `efficiency` and `breakthroughSuccess` bounded with §11.6 rows; `max`/`regen` uncapped and throwing on overflow.
- [ ] `move.range` registered; every range check passes with no board.
- [ ] `xpRate` layers on `Award.PowerScale`; `realm` still `1.0`.
- [ ] `git status tests/` clean.

---

## 9. Open questions

**One, deferred by design and named so it is not silently resolved.** Exhaustion debuff *magnitudes* —
how much a debuff actually removes — are a balance decision, not a structural one. This module ships
the channels and the composition; **values stay at defaults** per T7 (extract with values unchanged,
tune separately). The tuning pass belongs with whoever owns exhaustion feel, and it is not this
program.
