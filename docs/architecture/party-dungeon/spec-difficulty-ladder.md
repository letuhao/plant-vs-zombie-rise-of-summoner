# Spec: difficulty-ladder

Status: **APPROVED by the owner 2026-09-05 (wave 1) — unbuilt.** Verified against code the same day; every `file:line`
below was opened in this session. Every number is a starting shape chosen so the system runs, never a
balance decision (`ssot-power-scale.md` §5.3's own caveat applies to all of them).

Module id `difficulty-ladder` in the [party-dungeon map](../party-dungeon-map.md) (row 5). Depends on
`dungeon-registries` (tuning file, loader, rung-id registry). Reads `delve-graph-roll`'s row index and
the domain's base band. Feeds `encounter-generator` (Θ_room), `dungeon-loot` (Θ_room, reward columns),
`delve-attrition` (`permadeathFromRung`, Oath flag), `domain-catalog` (offers, once-entry flags, the
unlock), `delve-stage` (effective band name). Ideal: [party-dungeon-ideal.md](../party-dungeon-ideal.md)
§4.3, §4.6, §11.2, §8 box 1, §11.9 boxes 7/8/15, §11.10 R2/R4/R8/R12. Review:
[audit-2026-09-05.md](audit-2026-09-05.md) §1(c), §1(g), S2-1, S2-2, N6.

## Objective

Be the **first production composer of `Θ_content`** and the Delve's whole difficulty vocabulary while
adding **no power-shaped scale**: `ssot-power-scale.md` §10 stays at 27 rows (row 17 retired;
`inventory.json` 27 entries, counted 2026-09-05). A room's difficulty is an integer `DangerBand` handed
to the shipped `PowerIndexComposer`; a rung is a band delta plus rule modifiers owned by encounter
design and the economy; the tail is the same delta with no last value. Success looks like: a `rich`
domain on `hard` composes `Θ_room = 70` in its first row and `100` in its boss room, every enemy HP,
item level and soul payout downstream reading that number once, and `guard-power.ps1` green.

## Locked anchors

- **Decision 1 (§8 box):** *"some specific dungeon hard, very hard difficult and above will permanent
  death — make them tunable."* Hence `permadeathFromRung`, per domain. Recovery in delves (R6) is
  `delve-attrition`'s; this module exposes the gate only.
- **Decision 7 (§11.9 box):** *"'Medium' sits one band below the entrance band. Rung 3 has `bandDelta`
  −1; `hard` (rung 4) is the authored band and becomes the identity row for modifiers … very-easy −2
  (floors at 0), easy −1, medium −1, hard 0, very-hard 0, nightmare +1, hell +1, abyss +2, hopeless +2,
  impossible +3; the tail starts from +3."*
- **Decision 15 (§11.9 box):** *"A one-run dungeon drops very strong items and has +7 difficulty"* —
  `domain.onceEntry.bandDelta`, *"a band delta feeding row 23, not a multiplier."*
- **R2:** *"the picker shows the effective band name, never the delta; the stack rule with rung deltas
  is written (it stacks)."* **R4:** *"A clear at `maxRungWithoutOath` itself opens the next rung. The
  Oath below the gate stays as opt-in permadeath and a first-clear key, not the unlock mechanism."*
  **R8:** *"Every rule rung carries a reward-bearing column; the delta column is unchanged … neighbouring
  rungs differ in `bandDelta` or a reward column, never only a penalty. `hard` is the identity row;
  modifiers the first table hung on `hard` move one rung up; `depth.bossBand` becomes
  `depth.bossBandDelta` on the last corridor's band; a rung whose band would clamp on a domain is not
  offered."* **R12:** *"`onceEntry.failKeepsBossLoot: true` and `onceEntry.sealOnWipe: true`, both
  tunables per domain."*
- **The one-ladder rule** (`ssot-power-scale.md` §10): *"A power-shaped number that is not in this
  table does not have permission to exist."* §10.3: *"Enemy level is `Θ_content` … enemy count is
  encounter design."* PS-3: contests read `Θ`, magnitudes read `P(Θ)`. PS-5: faucet and sink on the
  same read. PS-8: a cap on a magnitude is a progression ceiling until proven otherwise.

## Design

### 1. Θ composition — one call into the shipped composer

```text
band(room) = entranceBand                                  domain seed (PLANNED by the planner)
           + row / depth.rowsPerBandStep                   integer division, row 0-based
           + bandDelta(rung)                               §2
           + onceEntry.bandDelta     (once domains)        §5
           + tail.bandStepPerPlus·n  (tail)                §3
boss room:  band(lastCorridorRow) + depth.bossBandDelta     review §1(g)(5), N4
Θ_room = PowerIndexComposer.ContentExplain(tuning,
           new ContentContext(band, WorldTier, ZombossLevel, RealmsAdvanced)).Total
```

`ContentContext` is the four-field record at `ContentContext.cs:16`; `ContentExplain`
(`PowerIndexComposer.cs:63-75`) validates `Wf = Wa` (`:47-51`), throws `PowerWeightMissing` on a null
`Wm` (`:67-68`), weighs `dangerBand` by `WmMilli` (`:72`), sums in `checked` `long` and rounds once at
the sum (`:80-93`). `WorldTier`, `ZombossLevel`, `RealmsAdvanced` pass through from the parent world;
this module composes the band and nothing else. `ContentContext` has no production constructor today —
`WaveCatalog.cs:140-146` sets `Level = theta` from a content constant — so this is the first producer.

**Worked numbers** (`power-scale.v2.json`: `Wm 5000`, `Ww 5000`, `Wf 25000`, `Wz 1000`, `B = 0.4`,
`A = 26200` derived from the pin; `rowsPerBandStep 2`, `bossBandDelta 1`; a `rich` entrance, band 3 per
`SectorTypeCatalog.cs:70`; tier 1; two realms; zomboss 0; long tier, 11 rows `r = 0…10`):

| Room | Rung | band | `Θ_room` | `P(Θ_room)` | `contentScale` ‰ | Enemy | `Θ_enemy` | `MaxHp = P(Θ_enemy)` |
|---|---|---|---|---|---|---|---|---|
| row 0 fight | hard (0) | 3 | **70** | 2,880 | 4,235 | raider +13 | 83 | 3,616 |
| row 10 (last corridor) | hard | 8 | 95 | 4,355 | 6,404 | — | — | — |
| boss | hard | 8 + 1 = 9 | **100** | 4,680 | 6,882 | tyrant +27 | 127 | 6,608 |
| row 0 | impossible (+3) | 6 | 85 | 3,780 | 5,559 | raider | 98 | 4,549 |
| boss | impossible | 12 | 115 | 5,713 | 8,401 | — | — | — |
| row 0, once domain | hard, once +7 | 10 | 105 | 5,015 | 7,375 | — | — | — |
| row 0, once domain | impossible, once +7 | 13 | 120 | 6,080 | 8,941 | — | — | — |
| row 0, tail `abyss +5` | 10 + 5 | 11 | 110 | 5,360 | 7,882 | — | — | — |

`P(Θ)` is `PowerLadder.Value` (`PowerLadder.cs:34-58`); `contentScale` is `ContentScale.Milli`
(`ContentScale.cs:15-20`). `Θ_enemy = Θ_room + thetaOffset` is `encounter-generator`'s sum (offsets
`demon-threat.v1.json`, §10 row 18; shape `SpeciesExpander.cs:66-67`). Two corrections to the ideal's
worked line: *"boss at band 6 → Θ 98"* was under the retired absolute `depth.bossBand`; *"×5.32"* is
`contentScale(83)`, the soul faucet's read (`SoulEarnPolicy.cs:74-75`), not the loot pipeline's, which
reads the room (`LootPipeline.cs:171-175`, `source.ContentLevel` and nothing else).

### 2. The rung table

Rows are **absolute, not cumulative** — a balance pass reads one row; the validator compares whole
rows. Identity is `hard`: every ‰ column 1000 (markup 0), every delta 0, every flag off.

| # | Rung | `bandDelta` | Reward-bearing (R8) | Encounter design | Economy | Precedent |
|---|---|---|---|---|---|---|
| 1 | `very-easy` | **−2** | `elite.rarityShiftRungs −1` | `restWeightMilli 1500`, `unknownPityStepMilli 2000` | `hungerMilli 500` | DD Radiant |
| 2 | `easy` | **−1** | `elite.rarityShiftRungs −1` | — | `hungerMilli 750`, `wildJoinDeltaMilli +50` | — |
| 3 | `medium` | **−1** | `elite.rarityShiftRungs 0` | — | — | one band below authored |
| 4 | **`hard`** | **0** | — | — | — | **identity row** — the band as authored |
| 5 | `very-hard` | **0** | `enemyCountDelta.fight +1` | `eliteWeightMilli 1625` | `restHealMilli 750`; permadeath from here (default) | StS A1 (×1.6 elites), A5 |
| 6 | `nightmare` | **+1** | `enemyCountDelta.fight +1` | `eliteWeightMilli 1625` | `restHealMilli 750`, `merchantMarkupMilli 100`, `hungerMilli 1250` | StS A11/A16 |
| 7 | `hell` | **+1** | `enemyCountDelta.fight +1`, `boss.rarityShiftRungs +1` | as 6 + `eliteKitTier 2`, `restEveryOtherRow` | as 6 + `eventSeverityTier 1` | Dead Cells BC1, StS A15/A18 |
| 8 | `abyss` | **+2** | as 7 | as 7 + `bossRetinuePerPartyDelta +1`, `bossWDelta +2` | as 7 + `spiritDrainMilli 1500` | Hades Extreme Measures |
| 9 | `hopeless` | **+2** | `enemyCountDelta.fight +1`, `.elite +1`, `boss.rarityShiftRungs +2` | as 8 + `bossKitTier 3`, `restRowsOnlyBeforeBoss` | as 8 | Dead Cells BC2/BC4, StS A19 |
| 10 | `impossible` | **+3** | `enemyCountDelta.fight +2`, `.elite +1`, `boss.rarityShiftRungs +3` | as 9 + `doubleBoss`, `eliteSecondActionRow` | as 9 | StS A20 |

**Ownership**, per §10.3 (ideal §11.2): **power** owns `bandDelta` and nothing else — no hp/atk/damage/
yield column exists and the validator refuses one by name; **encounter design** owns counts, retinue,
`W`, double boss, kit tiers, the second action row, node weights, rest-row flags; **economy** owns
hunger, rest heal, spirit drain, markup, event severity, wild join; **loot** owns the `rarityShiftRungs`
columns, written here and read in `dungeon-loot`. The reward-bearing set is exactly
`enemyCountDelta.{fight,elite}` and `loot.rooms.{elite,boss}.{rarityFloor,rarityShiftRungs}` — more
kills pay more `KillEarn`, a shifted table pays better items. Each twin now differs: `easy`/`medium` in
the elite loot band (easy trades a loot band for cheaper hunger — a trade, not dominance);
`hard`/`very-hard` in `enemyCountDelta.fight`; `nightmare`/`hell` and `abyss`/`hopeless` in
`boss.rarityShiftRungs`. The StS-A1 elite bump sits on `very-hard` (review §1(g)(3)).

**Validator** (`RungValidator`, throws at load): ten rows, registry ids, rungs 1…10 contiguous; `hard`
at identity in every column; every consecutive pair differs in `bandDelta` **or** a reward-bearing
column; no column named `hp`, `atk`, `damage`, `yield`, `theta`, `thetaOffset`, `multiplier`, `scale`;
none naming an actor axis (`dave`, `realms`, `runs`, `level`); no key containing `Day`, `Hour`,
`Minute`, `Ms`, `Time`; `bandDelta` non-decreasing; `permadeathFromRung` names a rung.

### 3. The tail — "impossible" is a name, not a ceiling

`difficulty.tail`: `enabled true`, `startsAfterRung 10`, `bandStepPerPlus 1`, `labelFormat "abyss
+{n}"`, `rulesFrozenAtRung 10`. Past rung 10 only the band moves; the rule row is rung 10's verbatim.
`n` is an `int ≥ 1` picked at commit among the offered steps (§4). No upper bound in the file —
`enhancement.v1.json:22`'s `toLevel: null` is the precedent. The **only** absolute bound is
`PowerLadder.MaxIndex` (`PowerLadder.cs:65-84`, a computed property of the loaded curve — 214,748,299
at `B = 0.4`, recomputed this session), which `Guard` turns into a `PowerIndexOverflow` throw
(`:100-106`). The picker rejects an `n` **before composing** when the would-be `Θ`, computed in `long`
with the same weighted sum, exceeds `MaxIndex`; the composer's `checked` cast (`:93`) is the backstop.
`+1` band is `+5 Θ`, ≈ +8–9 % on `contentScale` around Θ 70–100 and flattening as `P` grows.

### 4. Permadeath gate and the Oath unlock

`difficulty.permadeathFromRung: 5` (`very-hard`); a domain seed may carry `permadeathFromRung` to
override it (absence means the difficulty default — a fallback to another tunable, never a built-in
number). `PermadeathGate.Applies(domain, rung) = rung ≥ effectiveGate`. What permadeath *does*
(`downedOnce`, Retired at extraction, the wipe rule, R3) is `delve-attrition`'s.

**The Oath** is a commit-time flag on rungs *below* the gate: the same permadeath rule plus a
first-clear record `(playerId, domainId, rungId, oath: true)`. It unlocks nothing (R4). **Unlocking is
by clearing:** rungs `1…domain.maxRungWithoutOath` (starting 8, `abyss`) are offered freely; rung
`r + 1` is offered once a clear at `r` exists for `(playerId, domainId)`, for every `r ≥
maxRungWithoutOath`; the tail follows the same rule — `abyss +1` needs a clear at rung 10, `abyss +n` a
clear at `+(n−1)`. That is R4 applied to every step above the free band; the one reading this spec adds
is that the tail is not exempt. Owner correction is one line in `OathUnlock`.

### 5. Once-entry flags and the stack rule

`domain.onceEntry.bandDelta 7`, `sealOnWipe true`, `failKeepsBossLoot true`; `bossRarityFloor` is
`dungeon-loot`'s key, named here only because the confirm dialog cites it. **Stack rule (R2):** `+7`
**stacks** with rung and tail — `band = entrance + rowStep + bandDelta(rung) + 7 [+ n]` — so a once
domain at band 3 on `impossible` starts at band 13 (`Θ 120`), a +50 Θ gap against the same party.
Nothing caps the picker; the player sees the **effective band name** (§6), never `+7`. Sealing and the
loot half of a wipe are `domain-catalog`'s and `dungeon-loot`'s; this module exposes the flags on `RungOffer`.

### 6. Refuse-not-clamp and the picker's effective band

`PowerIndexComposer.ClampNonNegative` (`:77-78`) floors a negative axis to 0 under the comment *"a
missing progression row is absence, not corruption"*. A `−2` delta on a band-1 domain is neither
(S2-2). This module never reaches it: a rung is **offered** only when its effective entrance band —
`entrance + bandDelta [+ onceEntry.bandDelta]` — is `≥ difficulty.minOfferedBand` (starting 1; band 0
is `SectorTypeCatalog.cs:24`'s "safe ground", and a delve is never safe ground). Otherwise the rung is
absent with a reason the stage can show (`RungOfferRefusal.BandBelowFloor`). A band-1 domain offers
`hard` and up; band 2 offers `easy` and up; from band 3 the whole ladder shows — why the planner assigns
`many` domains `dangerBand ≥ 2` (review §1(g)(4)). `EffectiveBandName(band)` maps the composed entrance
band to a name from the `bandNames` registry (`dungeon-registries`; past the list, the last name with
`+k`) — the picker gets a name and a Θ-free description, never a delta.

### 7. The contest table and the actor-side wiring gap

Recomputed this session from `BattleModels.cs:239-242` (`BaseAccuracy 220 + 26θ`, `BaseDodge 26θ`,
`BaseCritRate 10θ`, `BaseCritResist 10θ + 250`), `stats.v1.json:11` (`accuracyScale 100`),
`CombatProbability.cs:8-9` → `ResistanceEvaluator.cs:123-124` (logistic, steepness 1). `gap =
Θ_content − Θ_actor`; one band is `Wm = 5 Θ`.

| gap Θ | bands | our hit | their hit | our crit | their crit | Reached by |
|---|---|---|---|---|---|---|
| 0 | 0 | **0.900** | 0.900 | 0.076 | 0.076 | parity — `σ(2.2)`, locked by the rate tests |
| +5 | 1 | 0.711 | 0.971 | 0.047 | 0.119 | `nightmare`/`hell` on a matched party |
| +10 | 2 | 0.401 | 0.992 | 0.029 | 0.182 | `abyss`/`hopeless` |
| +15 | 3 | 0.155 | 0.998 | 0.018 | 0.269 | `impossible` |
| +20 | 4 | 0.047 | 0.999 | 0.011 | 0.378 | `abyss +1` |
| +35 | 7 | 0.001 | 1.000 | 0.002 | 0.731 | once-entry `+7` alone |
| +50 | 10 | 0.00002 | 1.000 | 0.0006 | 0.924 | once-entry on `impossible` |

The honesty statement: **a rung is a wall exactly while the gap stands**, and PS-8 holds only because
the gap can be closed. In the SSOT's composition (§5) realms cancel (`Wf = Wa`, enforced at
`PowerIndexComposer.cs:47-51`), so Dave level (`Wd = 1`) and runs (`Wr = 0.25`) close it — `+35` is
thirty-five Dave levels, the D3 Torment shape. **The wiring gap:** today the squad's contest side reads
the **specimen level** — `BattleStatComposer.cs:108` `int theta = setup.Index;`, `BattleModels.cs:24`
`Index => Level`, filled at `WebMatchService.cs:396-403` from `s.Actor.Level`. **The seam this module
proposes** (not builds): the delve host composes `Θ_actor` per party member through
`IPowerIndexProvider.ActorIndex(StatContext)` (`IPowerIndexProvider.cs:15`; `HydratedPowerIndexProvider`
`:50`) and passes it on `BattleActorSetup` — `Index` is a read-only alias of `Level` (`:24`), so
`delve-battle-profile` and `power-index` hydration (`spec-power-index.md` §2.5) decide whether that is
`Level` or a new init-able field; it moves `BaseHp(level)` at `WebMatchService.cs:407` too and is
theirs. **Fallback until it lands:** the gap closes on §10 row 27's cost ladder — **35 specimen levels
per demon per +35 Θ**. The ladder never moves with the player: a `bandDelta` tied to `Θ_actor` is Last
Epoch corruption (ideal §11.2); the validator's actor-axis ban enforces that.

### 8. The reward read and PS-5

Every reward reads `contentScale(Θ_room)` once through its shipped funnel: item level via `LootPipeline`
(`:175` reads the synthesized `LootSourceRow.ContentLevel = Θ_room`, `dungeon-loot`'s host); souls per
kill via `SoulEarnPolicy.KillEarn(Θ_enemy)` (`:74-75`); victory souls **once per delve at extraction on
`Θ_run`** (S1-1). Drop *count* reads `Θ_actor` (`LootPipeline.cs:192`, row 28) — untouched here. **Sinks
on the same read (PS-5):** provisioning is priced on `contentScale(Θ_room at row 0)` — what the first
room's kill pays on (ideal §11.2's `Θ_entrance + Wm·bandDelta`, equal because `rowStep(0) = 0`); the
merchant prices on **the room it stands in** (N6 — the shallowest Θ would price the deepest faucet at a
2.5× discount); `merchantMarkupMilli` is a ‰ on that price, never a curve. **First clear** is per
`(playerId, domainId, rungId)` with the tail label as the rung id — a discrete unlock, not a volume cap.

## Tunables

File `data/tuning/dungeon.v1.json` — schema and loader are `dungeon-registries`' (T5 rejection on any
missing key). `domain.*` rows are defaults here and per-seed overrides on the domain anchor
(`dungeon-seed-contract`, PLANNED/VALIDATED levels).

| Key | Unit | Owner | Starting shape |
|---|---|---|---|
| `difficulty.rungs[].{id,rung}` | registry id, 1…10 | this module | the ten ids of §2 |
| `difficulty.rungs[].bandDelta` | bands (int) | power | −2 −1 −1 0 0 +1 +1 +2 +2 +3 (decision 7, verbatim) |
| `difficulty.rungs[].enemyCountDelta.{fight,elite}`; `.loot.rooms.{elite,boss}.rarityShiftRungs`, `.boss.rarityFloor` | count; bands / rarity ordinal | encounter; loot | §2 — the reward-bearing set |
|`difficulty.rungs[].provisionCellsDelta`|cells int, signed; `hard` = 0|`loot-pack` §4 (added 2026-09-05, wave 3): +4 +2 0 0 0 −2 −2 −4 −4 −6 from `very-easy` to `impossible` — a penalty column under R8 (never the only difference between neighbours), one more `RungDef` column; registries already declares it|—|
| `difficulty.rungs[].{eliteWeight,restWeight,unknownPityStep}Milli` | ‰ of base weight | encounter | 1000 identity |
| `difficulty.rungs[].{bossRetinuePerPartyDelta,bossWDelta}`; `.{eliteKitTier,bossKitTier}` | count; ordinal | encounter | 0; 1 identity |
| `difficulty.rungs[].{doubleBoss,eliteSecondActionRow,restEveryOtherRow,restRowsOnlyBeforeBoss}` | bool | encounter | false |
| `difficulty.rungs[].{hunger,restHeal,spiritDrain}Milli`; `.merchantMarkupMilli`, `.wildJoinDeltaMilli`; `.eventSeverityTier` | ‰; ‰ delta; ordinal | economy | 1000; 0; 0 |
| `difficulty.permadeathFromRung`; `difficulty.minOfferedBand` | rung; band | this module | 5 (`very-hard`); 1 |
| `difficulty.tail.{enabled,startsAfterRung,bandStepPerPlus,labelFormat,rulesFrozenAtRung}` | bool / rung / bands / text / rung | this module | true / 10 / 1 / `"abyss +{n}"` / 10 |
| `depth.rowsPerBandStep`; `depth.bossBandDelta` | rows per band; bands | this module | 2 (replaces the prose *"1 per 2 rows"*, S2-2); +1 on the last corridor's band (replaces absolute `depth.bossBand: 6`, N4) |
| `domain.maxRungWithoutOath`; `domain.permadeathFromRung` | rung; rung (optional) | this module, per-seed override | 8; absent → `difficulty.permadeathFromRung` |
| `domain.onceEntry.{bandDelta,sealOnWipe,failKeepsBossLoot}` | bands / bool / bool | this module, per-seed | 7 / true / true |

Validators: a unit in every key name (T6); the band-shaped keys are `int`; `rowsPerBandStep ≥ 1`,
`bandStepPerPlus ≥ 1`, `startsAfterRung == rulesFrozenAtRung == 10`, `labelFormat` contains `{n}`; no
`boss-lair` copy anywhere (N13 — `SectorTypeCatalog.cs:98`'s 6 is the map's, never read here).

## Numeric types

| Quantity | Type | Why |
|---|---|---|
| band, `bandDelta`, `rowStep`, `n`; `Θ` | `int` | counts of bands, widened to `long` inside the composer (`:84-91`); `ContentContext`/`PowerAxisReport.Total` are `int` and the `checked((int)…)` at `:93` throws past it |
| `P(Θ)`, `contentScale` numerator, souls, ‰ columns | `long` | `PowerLadder.Value` / `ContentScale.Apply` — the repo's `long`-for-magnitudes rule (`ssot-power-scale.md` §4); widen before multiplying, divide by 1000 last, once; never `float`/`double` |
| `MaxIndex` | `long` | `PowerLadder.MaxIndex` — read, never restated as a literal; overflow throws (tail pre-check refuses, `Guard` throws, nothing clamps) |

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Delve.Difficulty"
.\scripts\guard-power.ps1        # green 2026-09-05; stays green with inventory.json byte-identical (§10 unchanged)
python scripts\audit-magic-numbers.py --domain dungeon     # no M1 in Delve/Difficulty
```

## Structure

```
src/FusionRpg.Core/Delve/Difficulty/
  RoomTheta.cs         Compose(...) → RoomTheta { ContentContext Context; int Theta; int Band }
  RungTable.cs         RungDef + loaded rows       RungValidator.cs   §2's rules (throws RungTableRejection)
  RungOffer.cs         offers per (domain, player): refuse-not-clamp, unlock state, once flags, band name
  TailLadder.cs        n → band, MaxIndex pre-check  PermadeathGate.cs  OathUnlock.cs
tests/FusionRpg.Core.Tests/Delve/Difficulty/   goldens, validator red/green, contest property test
```

Nothing under `Core/Power` changes; no `Data/` or `Server/` here — clears persist on `delve-scope`'s
header row, the endpoint is `domain-catalog`'s.

## Code style

Pure, tuning-injected, no I/O (tunables-ssot §7.2). No parameter named `level`/`lvl`/`index` on a
numeric-returning method — `guard-power.ps1`'s G2 heuristic is the tripwire; no allowlist entry needed.
The composition returns a record and delegates every weighted sum to the composer:

```csharp
public static RoomTheta Compose(PowerTuning power, DifficultyTuning diff, DomainDef domain,
    RungDef rung, int row, int tailPlus, bool isBoss, ParentWorldTerms world)
{
    // int counts of bands; the composer widens to long (PowerIndexComposer.cs:84-91).
    int band = checked(domain.EntranceBand + row / diff.Depth.RowsPerBandStep + rung.BandDelta
        + (domain.Entry == DomainEntry.Once ? domain.OnceEntry.BandDelta : 0)
        + tailPlus * diff.Tail.BandStepPerPlus + (isBoss ? diff.Depth.BossBandDelta : 0));
    if (band < diff.MinOfferedBand)
        throw new RungNotOffered(rung.Id, band);            // §6 — refuse, never reach ClampNonNegative
    var ctx = new ContentContext(band, world.WorldTier, world.ZombossLevel, world.RealmsAdvanced);
    return new RoomTheta(ctx, PowerIndexComposer.ContentExplain(power, ctx).Total, band);
}

public static void Validate(IReadOnlyList<RungDef> rungs)
{
    for (int i = 1; i < rungs.Count; i++)
        if (rungs[i - 1].BandDelta == rungs[i].BandDelta
            && !RewardBearing.Any(col => col.Read(rungs[i - 1]) != col.Read(rungs[i])))
            throw new RungTableRejection($"{rungs[i - 1].Id}/{rungs[i].Id}: neighbours differ only in penalty columns (R8)");
}
```

## Testing strategy

- **Composition goldens:** §1's table — `(3, hard, row 0) → 70`, boss of 11 rows `→ 100`, impossible
  row 0 `→ 85`, once `+7` on hard `→ 105`, `abyss +5 → 110` — each through `ContentExplain`. (Corrected
  2026-09-05, `RoomThetaComposerTests`: the table's other six rows all sit on an exact 5-Θ-per-band
  rate above the band-3/Θ-70 anchor; band 11 on that line is 110, not the previously stated 125 — a
  transcription slip, not a composer defect. `P(Θ)`/`contentScale` corrected to 5,360/7,882 to match.)
- **Refuse-not-clamp:** band-1 offers `hard`…`impossible` only; band-2 offers `easy` and up; every
  offered rung composes a band `≥ 1`, so `ClampNonNegative` is never reached.
- **Validator red/green:** twins (`easy` with `elite.rarityShiftRungs 0`) rejected; a penalty-only
  neighbour (`hell` minus its `boss.rarityShiftRungs`) rejected; `hpMilli`, `daveLevelDelta` and
  `restCooldownMinutes` rejected by name; the shipped table passes.
- **Tail:** an `n` whose `Θ` exceeds `PowerLadder.MaxIndex` is refused before `ContentExplain` runs;
  `n = 1` on rung 10 composes one more band; `labelFormat` renders `"abyss +3"`.
- **Oath unlock:** a clear at rung 8 opens 9; an Oath clear at `very-easy` opens nothing and writes a
  first-clear row with `oath: true`; a clear at rung 10 opens `abyss +1`.
- **§10 unchanged:** `inventory.json` has 27 entries and no `location` under `Core/Delve` — the
  module's own copy of `guard-power`'s G3.
- **Contest property test:** for gaps 0/5/10/15/20/35, `CombatProbability.Sigmoid((BaseAccuracy(θ) −
  BaseDodge(θ + gap)) / accuracyScale, …)` reproduces §7 to ±0.001; parity stays `0.900 ± 0.02` for any
  `θ` — the SSOT §2 invariant, re-asserted because the tail makes large `θ` real.
- **One read for faucet and sink:** provisioning price and `KillEarn` at row 0 share one `contentScale`.

## Boundaries

**Always** — compose through `PowerIndexComposer.ContentExplain`; refuse a rung rather than clamp a
band; show the effective band **name**; stack once-entry with rung and tail; validate the table at load
and throw; read `MaxIndex` from `PowerLadder`; keep `hard` the identity row.

**Ask first** — moving a delta (decision 7 is the owner's); changing the reward-bearing column set; a
per-domain rung table (today one table, `domain.*` overrides only); exempting the tail from
clear-opens-next (§4).

**Never** — a `rungThetaOffset` column (two names for `Wm·Δband`); a multiplier on hp, atk, damage or
yield; a rung column naming an actor axis; a day/time key on any rung; a silent clamp anywhere; a new
`ssot-power-scale.md` §10 row; a `boss-lair` band copy; `Θ_actor` in `bandDelta`; a `float`/`double`
magnitude.

## Success criteria

1. §1's goldens hold and every Θ passes through `ContentExplain`. 2. `.\scripts\guard-power.ps1` green
with `inventory.json` byte-identical and §10 at 27 rows. 3. The validator rejects twins, penalty-only
neighbours, actor-axis and time keys, and accepts the shipped table. 4. No offered rung composes a band
below `minOfferedBand`. 5. A tail step past `MaxIndex` is refused before composing. 6. Clear-opens-next
proven for rung 9, rung 10 and `abyss +1`; the Oath unlocks nothing. 7. `audit-magic-numbers.py
--domain dungeon` reports no M1 in `Delve/Difficulty`.

## Interface exposed to dependents

| Member | Returns | Consumer |
|---|---|---|
| `RoomTheta.Compose(power, diff, domain, rung, row, tailPlus, isBoss, world)` | `RoomTheta { ContentContext, int Theta, int Band }` | `encounter-generator` (`Θ_room`), `dungeon-loot` (`ContentLevel`), merchant pricing |
| `RungTable.Get(rungId)` | `RungDef`, every column read-only | `encounter-generator`, `delve-attrition`, `dungeon-loot`, `event-deck` |
| `RungOffer.For(domain, playerClears)` | offered rungs and tail steps with `EffectiveBandName`, refusal reasons, once flags | `domain-catalog`, `delve-stage` |
| `PermadeathGate.Applies(domain, rung, oath)`; `OathUnlock.Opens(clear)`; `TailLadder.TryBand(n)` | `bool`; the next offered rung or tail step; band or `Refused(MaxIndex)` | `delve-attrition`; `domain-catalog`; `RungOffer` |

## Design-gate checklist

```
[x] Subsystems: power (Θ composition, ladder, caps), party dungeon, battle rates, economy (PS-5), tunables.
[x] Read this session: party-dungeon-map.md; ideal §4.2-4.3, §4.6, §4.9, §5, §8 box, §11.2, §11.9-11.10;
    audit §1(c), §1(g), S1/S2, §4-§9; ssot-power-scale.md §2, §4, §5, §9, §10, §11, §12; power-map.md;
    spec-content-scale.md §6; spec-expeditions.md (format); tunables-ssot.md; DESIGN-GATE.md §5.
    decisions.md rows 113-115 checked; nothing locks Θ composition beyond the SSOT's PS rules.
[x] Every claim cites file:line, opened this session. Drift against the brief: PowerLadder.cs is 118 lines
    (MaxIndex :65-84, Guard throws :100-106, not :244-262); ContentContext.cs is 16 lines (no :138);
    BaseAccuracy family is BattleModels.cs:239-242 (corrected 2026-09-05); BattleActorSetup.Index is a get-only alias of Level
    (:24), so "pass Θ_actor on Index" needs a field decision (§7).
[x] Verified against CODE, not comments — every file named in §1-§8 plus guard-power.ps1, the four
    tuning files and inventory.json. Surrounding sections read for every quoted rule (§10.3 with §10
    whole; ClampNonNegative's comment with its call sites; R4 with the decision 8 it amends).
[x] Constraints tested: guard-power.ps1 run (green); MaxIndex 214,748,299 and the contest table recomputed
    numerically; inventory.json counted (27). No dotnet suite run — this spec changes no code.
[x] No §2 invariant contradicted. One reading added, named in §4: the tail follows clear-opens-next.
[ ] Propagation owed, not done here (one-file task): ideal §11.2's worked line (boss Θ 98 under the
    retired absolute band; ×5.32 is the faucet's read); ideal §5 `depth.bossBand` → `depth.bossBandDelta`,
    `depth.bandStepPerRow` → `depth.rowsPerBandStep`.
```
