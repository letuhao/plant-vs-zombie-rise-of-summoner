# Effort → power reconciliation — every ladder in the repo, audited against §10.5

**Question (owner, 2026-09-05):** *"i think our power ladder and effort spent is scattered, maybe we
need reconcile them for balance and persistency"*

The concrete instance that prompted it is fixed: D26 reconciled the passive tree's tier requirement
(`t(t−1)/2`) to its power index (`t(t+1)/2`), turning *"flat within 11%"* into `b/5` exactly
([passive-tree-ideal.md:57](../../architecture/passive-tree-ideal.md)). This audit asks whether that
class of mismatch exists elsewhere.

**Answer: yes, in five places, and one of them is live shipped code.** Every claim below cites
`file:line` and is marked FACT (read in code this session) / INFERENCE (algebra over facts) /
RECALL (from a doc read this session).

**The property being audited** ([ssot-power-scale.md §10.5](../../architecture/power/ssot-power-scale.md)):

```text
cumulative cost   Σ(first + (k−1)·step)  ≈ (step/2)·L²      quadratic in L
power at index    C + A·Θ + B·Θ(Θ−1)/2   ≈ (B/2)·Θ²         quadratic in Θ
                  ⇒ power ∝ total investment                LINEAR in effort
```

With the shipped dial (`cMilli 80000, bMilli 400, pinIndex 20, pinValue 680` —
[data/tuning/power-scale.v2.json:9](../../../data/tuning/power-scale.v2.json)), `A` derives to 26,200‰
and `P(Θ) = 80 + 26.2·Θ + 0.2·Θ(Θ−1) ≈ 0.2·Θ²`. **FACT** — checked against §4.5's own table
(`P(1000) = 80 + 26,200 + 199,800 = 226,080`).

---

## 1. The classification table

`E` = effort (kills, battles, attempts — whatever the ladder's own clock is). The verdict is the shape
of **reward per unit of effort**.

| # | Ladder | Cost of the Nth unit | Reward at index N | Reward per effort | Verdict |
|---|---|---|---|---|---|
| 1 | **Player XP → level → Θ → P(Θ)** | `100 + 45(N−1)` XP, at a flat 12 XP/kill ([RpgProgression.cs:49-56](../../../src/FusionRpg.Core/Progression/RpgProgression.cs), [progression.v1.json:9-20](../../../data/tuning/progression.v1.json)) | `P(L) ≈ 0.2L²` ([PowerLadder.cs:34-38](../../../src/FusionRpg.Core/Power/PowerLadder.cs)) | `0.2L² / 1.875L² ≈ 0.107` per kill, constant | **LINEAR** ✅ the §10.5 promise, exactly |
| 2 | **Specimen level → P(Θ)** | **flat 100 XP, every level** ([RpgStore.UniqueActors.cs:872-877](../../../src/FusionRpg.Data/Sqlite/RpgStore.UniqueActors.cs)) | `P(level)` — the same ladder ([BattleModels.cs:169-175](../../../src/FusionRpg.Core/Battle/BattleModels.cs), fed `s.Actor.Level` at [WebMatchService.cs:339-352](../../../src/FusionRpg.Server/WebMatchService.cs)) | `2×10⁻⁵ · E` — rises linearly forever | **ACCELERATING** ⛔ live defect |
| 3 | **Soul faucet** | — (a faucet) | `KillDelta × contentScale(Θ) ∝ Θ²` ([SoulEarnPolicy.cs:74-80](../../../src/FusionRpg.Core/Demons/SoulEarnPolicy.cs)) | quadratic in depth | **ACCELERATING** — inert today, Θ pinned at 20 ([RpgStore.Souls.cs:29](../../../src/FusionRpg.Data/Sqlite/RpgStore.Souls.cs)) |
| 4 | **Soul sinks** (pull / slot / ritual / upkeep / fusion) | pull `100` flat ([summoning.v1.json:11](../../../data/tuning/summoning.v1.json)); slot `300(n+1)` ([ContractPolicy.cs:171-172](../../../src/FusionRpg.Core/Demons/Contracts/ContractPolicy.cs)); ritual, upkeep, promotion — rarity tables ([contracts.v1.json](../../../data/tuning/contracts.v1.json), [fusion.v1.json](../../../data/tuning/fusion.v1.json)) | +1 pull / +1 slot / +1 rung | **every sink is Θ-free** | **FLAT** ⛔ mismatched to row 3 |
| 5 | **Contract slots** | `300N` souls, cumulative `150N²` | +1 roster slot, each worth `P(Θ)` once filled | slots `∝ √(souls)`; legion power `∝ √E · P(Θ)` | **ACCELERATING** (compound of 3 + 4) |
| 6 | **Aptitude points** | `3` per Θ, linear, uncapped ([PointBudget.cs:31-39](../../../src/FusionRpg.Core/Stats/Aptitudes/PointBudget.cs), [aptitudes.v5.json:23-28](../../../data/tuning/aptitudes.v5.json)) | `k · share^γ · P(Θ)`, and `share` is `points_i / Σpoints` ([AptitudeReadFunctions.cs:48-68](../../../src/FusionRpg.Core/Stats/Aptitudes/AptitudeReadFunctions.cs), [AptitudeAllocation.cs:81-85](../../../src/FusionRpg.Core/Stats/Aptitudes/AptitudeAllocation.cs)) | **zero** at a fixed distribution | **FLAT-CAPPED** — deliberate; it is a distribution dial, not a magnitude ladder |
| 7 | **Action unlock rungs** | attempts to the Nth earn `= 1/chance(N−1)`, `chance = 500‰·0.88ⁿ` floored at `1‰` ([UnlockLadder.cs:41-53](../../../src/FusionRpg.Core/Actions/Unlock/UnlockLadder.cs), [action-unlock.v1.json:11-16](../../../data/tuning/action-unlock.v1.json)) — geometric, then ~1,000 attempts each past earn 50 | `rung = min(n, 10)`; `qPower = 1.75^((r−1)/2)` ([action-rungs.v2.json:6](../../../data/tuning/action-rungs.v2.json)) | rises to rung 10, then **zero** | **FLAT-CAPPED** — documented soft window (§11.2) |
| 8 | **Enhancement `+X`** | expected attempts `= 1/success(n)`, `1000‰ → 200‰` then held ([EnhancePolicy.cs:69-77](../../../src/FusionRpg.Core/Items/Mutation/EnhancePolicy.cs), [enhancement.v1.json:20-22](../../../data/tuning/enhancement.v1.json)); the material cost curve is **not built** | `gain(n) = cap·n/(n+8)` — asymptotic ([EnhancePolicy.cs:85-91](../../../src/FusionRpg.Core/Items/Mutation/EnhancePolicy.cs)) | marginal gain `∝ 1/n²`, total bounded by `cap` | **DECAYING** — deliberate (§4a), but has **no §10 row** ⛔ |
| 9 | **Star merge** | `SacrificesForStar(n) = n+1`, cumulative `n(n+3)/2` ([StarPolicy.cs:28-33](../../../src/FusionRpg.Core/Demons/Fusion/StarPolicy.cs)) | `30‰ · star · BaseAtk(level)` — **linear in star** ([fusion.v1.json:18](../../../data/tuning/fusion.v1.json), [WebMatchService.cs:369-382](../../../src/FusionRpg.Server/WebMatchService.cs)) | `60/(n+3)` — a 2× worse deal at star 5 than star 1 | **DECAYING** — the same index mismatch as D20, bounded at 5 rungs |
| 10 | **Demon rarity promotion** | **flat** `200` souls / 3 shards / 3 essence at every rung ([fusion.v1.json:20](../../../data/tuning/fusion.v1.json)) | +1 rung → higher star cap and fusion slots | rises with rung | **ACCELERATING** — and it contradicts `recipeCost` in the same file, which escalates `150 → 1000` |
| 11 | **Loam development** | authored `5`/level upkeep ([LoamPolicy.cs:52-53](../../../src/FusionRpg.Core/World/Loam/LoamPolicy.cs)) | yield `6`/level ([DevelopmentYield.cs:26-32](../../../src/FusionRpg.Core/World/Growth/DevelopmentYield.cs)) | net **+1 per level**, flat forever | **LINEAR** ✅ and `Θ`-invariant by decision (§10.4) |
| 12 | **Loyalty** | `15`/win, capped `60`/UTC day, decay `25`/day ([ContractPolicy.cs:141-154](../../../src/FusionRpg.Core/Demons/Contracts/ContractPolicy.cs), [contracts.v1.json:9-24](../../../data/tuning/contracts.v1.json)) | flat band bonuses `0/15/35/60‰` | bounded 0–1000 | **FLAT-CAPPED** — bounded ratio, §11.6 exempt |
| 13 | **Affix tier ladder** | — (generation-time) | `m₁ × 1.75^(t−1)`, 5 rungs ([FamilyExpansion.cs:82-91](../../../src/FusionRpg.Core/Effects/Atoms/Generation/FamilyExpansion.cs)) | n/a — relative, level-free | **documented exception**, §10 row 7 ✅ |
| 14 | **Ilvl → tier gate** | — | `MaxTierAt(ilvl)`, steps at 1/8/18/32 ([IlvlTierLadder.cs:10-22](../../../src/FusionRpg.Core/Items/IlvlTierLadder.cs)) | n/a — a gate | **documented exception**, §10 row 14 ✅ |
| 15 | **Drop volume** | — | `base + slope·(Θ − pin)` — **linear in Θ** ([DropVolume.cs:35-42](../../../src/FusionRpg.Core/Items/Drops/DropVolume.cs)) | items/hour `∝ √E` | **DECAYING in count, deliberate** (D18) — but has **no §10 row** |
| 16 | **The XP cost ladder itself** | `first + (L−1)·step` | — | — | **exempt by name**, §10 row 6 ✅ |

**Ladders that do not exist.** `element_mastery` — zero code, three doc-comment hits
([AptitudeTuning.cs:20](../../../src/FusionRpg.Core/Stats/Aptitudes/AptitudeTuning.cs),
[PointBudget.cs:13,15](../../../src/FusionRpg.Core/Stats/Aptitudes/PointBudget.cs)). Almanac XP — zero
code hits repo-wide; `Almanac` in `src/` is a rarity rung
([DemonRarityLadder.cs:12](../../../src/FusionRpg.Core/Demons/DemonRarityLadder.cs)), not a counter.
Status mastery — three hits, all under `docs/research/`. **FACT**, greps run this session.

---

## 2. Index mismatches — the defects, sharpest first

### M1. Specimen level is a flat-cost ladder feeding a quadratic reward ⛔ LIVE

**FACT.** [RpgStore.UniqueActors.cs:872-877](../../../src/FusionRpg.Data/Sqlite/RpgStore.UniqueActors.cs):

```csharp
var xp = row.Xp + delta;
var level = row.Level < 1 ? 1 : row.Level;
while (xp >= 100.0)
{
    xp -= 100.0;
    level++;
}
```

**FACT.** That `level` reaches the shared ladder unchanged.
[WebMatchService.cs:339,350-352](../../../src/FusionRpg.Server/WebMatchService.cs) passes
`s.Actor.Level` into `BattleRuleset.BaseHp/BaseAtk/BaseDefense`, and
[BattleModels.cs:169-175](../../../src/FusionRpg.Core/Battle/BattleModels.cs) says *"`level` is
treated as Θ directly"* and calls `PowerLadder.Value(level)`.

**INFERENCE — the algebra.** Player: cumulative XP to level `L` is `(L−1)(45L+110)/2 ≈ 22.5L²`, so
`P(L) ∝ L²` is linear in XP. Specimen: cumulative XP to level `L` is `100L`, so
`P(L) ≈ 0.2L² = 2×10⁻⁵·XP²` — **quadratic in effort.** One reward function, two cost indices:

| Level | Player XP | Specimen XP | Ratio |
|---|---|---|---|
| 20 | 1,900 | 2,000 | 0.95× |
| 100 | 227,450 | 10,000 | **22.7×** |
| 1,000 | 22.5 M | 100,000 | **225×** |

The gap is not a constant a balance pass can absorb — it grows linearly with level, which is what
makes this a shape defect rather than a tuning one. It is D26's mismatch one order larger: there the
two indices differed by one step; here one is triangular and the other is flat.

**This is a shipped path.** [ExpeditionEndpoints.cs:137](../../../src/FusionRpg.Server/ExpeditionEndpoints.cs)
awards `SpecimenXpPerBattleWon × XpMilli` per won battle through `AwardUniqueActorXpUnlocked`, and the
code is present in `HEAD`, not only in the working tree.

**The fix is a shape change, not a new system.** Specimen levels read `RpgXpCurve.XpToNext` with their
own `(first, step)` row in `progression.v1.json`, exactly as `plant` and `zombie` already do
([RpgProgression.cs:35-41](../../../src/FusionRpg.Core/Progression/RpgProgression.cs)). The curve, the
loader, the `checked` arithmetic and the tuning discipline all exist; the specimen path never used
them.

### M2. The soul faucet scales on `P(Θ)`; every soul sink is `Θ`-free ⛔ LATENT

**FACT.** The faucet: [SoulEarnPolicy.cs:74-80](../../../src/FusionRpg.Core/Demons/SoulEarnPolicy.cs)
multiplies `KillDelta` / `VictoryDelta` / `DefeatDelta` by `ContentScale.Milli(Θ, tuning)`.

**FACT.** Every sink, checked one by one: summon pull `costPerPull: 100`
([summoning.v1.json:11](../../../data/tuning/summoning.v1.json)); contract slot
`SlotPriceStep × (n+1)` ([ContractPolicy.cs:171-172](../../../src/FusionRpg.Core/Demons/Contracts/ContractPolicy.cs));
loyalty ritual and daily upkeep, per-rarity tables ([contracts.v1.json:39-62](../../../data/tuning/contracts.v1.json));
star merge `50`, promotion `200`, recipes `150–1000` ([fusion.v1.json:19-31](../../../data/tuning/fusion.v1.json)).
**None reads `Θ`.** Corroborated by the complete `ContentScale.` call-site list — it reaches two
faucets and the item/atom instantiators, and no sink at all.

**RECALL + FACT.** [ssot-power-scale.md §10.4](../../architecture/power/ssot-power-scale.md) states
Rule PS-5 — *"Within one economy loop, faucet and sink scale on the same read, or neither does"* — and
justifies scaling souls precisely *"because they are spent against `P(Θ)`-scaled content."* That
premise was never delivered; the sinks were not migrated with the faucet.

**Why it has not bitten yet, and exactly when it will.**
[RpgStore.Souls.cs:29](../../../src/FusionRpg.Data/Sqlite/RpgStore.Souls.cs):

```csharp
const int VanillaPvzKillAndRunTheta = 20;
```

`contentScale(20) = 1.000`, so today's faucet is byte-identical to a flat one. The mismatch fires the
day a real `Θ_enemy` is supplied — the same latency pattern §6 documents for the status contest.
§11.7a's own table already shows the faucet reaching **92.8×** at Θ=500 while every price above stays
where it is: a clean win at Θ=500 pays ~12,987 souls against a 100-soul pull.

**This is §11.7's defect with the sign flipped.** That section removed a flat faucet facing a scaling
sink and called it *"starvation with a delay fuse."* What shipped is a scaling faucet facing flat
sinks — inflation with the same fuse.

### M3. Scaling kill XP by `contentScale` would turn the main line exponential ⛔ PRE-EMPTIVE

**FACT.** The hook is written and earmarked.
[RpgXpAwardMap.cs:18-20,37](../../../src/FusionRpg.Core/Progression/RpgXpAwardMap.cs): *"Today
`NoKillPowerScaleYet` is exactly 1.0 and this is the identity; it exists so that when content-scale
supplies a real multiplier the fraction dies here."*
[RpgProgression.cs:129-130](../../../src/FusionRpg.Core/Progression/RpgProgression.cs) says the same
(*"today … 1.0, tomorrow content-scale"*), and §10.1 row 4's verdict records that the deleted
`RpgXpPowerScale`'s *"documented future job … is `Θ_content`."*

**INFERENCE.** If kill XP becomes `12 × contentScale(Θ)` while `XpToNext` stays `100 + 45(L−1)`, then
kills per level `= 45L / (12 · 0.2L²/680) = 12,750/L` — the cost of a level *falls* as you climb.
`dL/dk = L/12,750` integrates to `L = L₀·e^(k/12,750)`: **exponential in effort**, the shape §2 and
§6.2 exist to forbid.

The rule that prevents it is PS-5 restated for XP: **if the XP faucet takes a `P(Θ)` read, `XpToNext`
takes the same read, or neither does.** Today neither does, which is correct. Writing that down costs
one sentence now and a re-balance later.

### M4. Star merge pays linearly for a triangular cost ⚠ LOW STAKES

**FACT.** Cost: `SacrificesForStar(n) = n + 1`
([StarPolicy.cs:28-33](../../../src/FusionRpg.Core/Demons/Fusion/StarPolicy.cs)), cumulative
`n(n+3)/2`. Reward: `perStarPowerMilli = 30`, applied as `30‰ · star · BaseAtk(level)`
([fusion.v1.json:18](../../../data/tuning/fusion.v1.json),
[WebMatchService.cs:369-382](../../../src/FusionRpg.Server/WebMatchService.cs)) — **linear in star**.

**INFERENCE.** Reward per sacrifice `= 30n / (n(n+3)/2) = 60/(n+3)`: 15 at star 1, 7.5 at star 5. This
is D20's tier-1 defect mirrored — there a triangular requirement faced triangular power misindexed by
one; here a triangular cost faces *linear* power. D26's fix applies verbatim: pair the triangular cost
with a triangular reward, `perStar · star(star+1)/2`, and reward-per-sacrifice is constant by
construction.

Bounded at 3–5 rungs ([fusion.v1.json:10-17](../../../data/tuning/fusion.v1.json)), so the damage is
capped. A correctness-of-shape note, not an urgent one.

### M5. Promotion is flat where recipes escalate ⚠

**FACT.** `promotionCost` is `{souls: 200, shardCount: 3, essenceCount: 3}` for **every** rung
([fusion.v1.json:20](../../../data/tuning/fusion.v1.json)), while `recipeCost` in the same file runs
`150 → 1000` souls across seven rungs (`:21-31`). Two routes to the same rung, one priced against the
rung and one not. Whichever is right, they cannot both be.

---

## 3. Cross-ladder commensurability — the passive tree's four gate quantities

The charter's sharpest question, and the audit confirms red team F5 in full.

**FACT — what each gate quantity actually is:**

| Gate | Scope | Source in code | Growth in effort |
|---|---|---|---|
| Aptitude points | `Commander` | `PointsFor(Commander, Θ_player, …) = 3·Θ` ([PointBudget.cs:31-39](../../../src/FusionRpg.Core/Stats/Aptitudes/PointBudget.cs)); the only production caller is [AptitudeEndpoints.cs:47-48,81-82](../../../src/FusionRpg.Server/AptitudeEndpoints.cs) | `∝ √E` (Θ = daveLevel, and level `∝ √XP`) |
| Element mastery | `Aspect` | **does not exist** — three comment hits, no counter, no store, no endpoint | — |
| Almanac XP | `DemonType` | **does not exist** — zero code hits repo-wide | — |
| Specimen level | `UniqueDemon` | `rpg_unique_actors.level`, flat 100 XP per level (M1) | `∝ E` |

**INFERENCE — one `req(t)` cannot be meaningful across these.** `req(6) = 105` under D26 means: 105
aptitude points concentrated in a single aptitude (reachable near Θ≈35 only if the player spends
*every* commander point there); 105 specimen levels (10,500 specimen XP, on a battle-reward clock);
and 105 units of two counters that have no accrual rule, no store and no endpoint. Two of the four
sources do not exist, and the two that do grow at **different exponents in effort** — `√E` against
`E`. A threshold is a statement about a quantity's scale, and these are four different scales.

**The rate table cannot fix it, and it is worth being explicit about why.**
[aptitudes.v5.json:23-28](../../../data/tuning/aptitudes.v5.json) ships
`{commander: 3, demonType: 4, aspect: 4, uniqueDemon: 6}`, and
[AptitudeTuning.cs:16-24](../../../src/FusionRpg.Core/Stats/Aptitudes/AptitudeTuning.cs) states that
*"this module ships the RATE table only, never the sources."* A rate **multiplies** a source; it
cannot equalise two sources whose growth exponents differ. `6 × E` and `3 × √E` diverge whatever the
two constants are.

Three ways to make one threshold meaningful, in increasing cost:

1. **Give every gate quantity the same cost index.** Fix M1 so specimen level is arithmetic-cost like
   the player line, and specify `element_mastery` and almanac XP with arithmetic-cost curves from the
   start. All four then grow `∝ √E`, one `req(t)` is comparable, and the rate table becomes a genuine
   fine-tune instead of a load-bearing conversion.
2. **Express `req(t)` per scope**, in that scope's own units — red team F5's own proposal. Cheapest to
   write, but it multiplies the tuning surface by four and hands a balance pass four independent dials
   that must be kept in sympathy by hand.
3. **Gate on `Θ` for every scope** and let points be a spending currency only. Most consistent with
   PS-3 (`Θ` is the one index) and with row 6's finding that aptitude points already buy nothing on
   their own — but it removes the "concentration opens depth" mechanic D10 wants.

**Option 1 also fixes a live defect**, which is why it heads the fix list.

**A second commensurability fact, because the tree is about to depend on it.** The aptitude read is
over a **share**, not a count
([AptitudeAllocation.cs:81-85](../../../src/FusionRpg.Core/Stats/Aptitudes/AptitudeAllocation.cs)):
doubling every aptitude's points leaves every channel value identical. So a passive-tree tier gate
would be the **first consumer to give an aptitude point absolute value.** That is a change to what a
point *is*, not merely a new reader of it, and it deserves to be named as one before D26's `req(t)` is
built against it.

---

## 4. Persistence

The 2026-09-04 XP fix has two surviving siblings.

### P1. The XP ledger still stores XP as `REAL` ⛔

**FACT.** [RpgStore.cs:355-388](../../../src/FusionRpg.Data/Sqlite/RpgStore.cs):

```sql
CREATE TABLE IF NOT EXISTS rpg_actor_progression ( … xp INTEGER NOT NULL DEFAULT 0, … );
CREATE TABLE IF NOT EXISTS rpg_xp_ledger        ( … xp_before REAL NOT NULL, … xp_after REAL NOT NULL, … );
```

The snapshot column was migrated to `INTEGER`; the ledger's two XP columns were not, and the reader
forces `GetDouble` on both, plus on the `INTEGER` `delta` column
([RpgStore.Progression.cs:382-383](../../../src/FusionRpg.Data/Sqlite/RpgStore.Progression.cs)). The
DTOs carry the `double` to the wire —
[RpgProgressionDtos.cs:59,63,65,82,95](../../../src/FusionRpg.Contracts/RpgProgressionDtos.cs) —
against a `RpgActorState.Xp` that is now `long`
([RpgProgression.cs:100](../../../src/FusionRpg.Core/Progression/RpgProgression.cs)). Same defect, same
rule (CLAUDE.md: *"never in a hashed or persisted path"*), one table over.

### P2. Specimen XP is a `double` written into an `INTEGER` column ⛔

**FACT.** [RpgStore.UniqueActors.cs:849,865,872,890](../../../src/FusionRpg.Data/Sqlite/RpgStore.UniqueActors.cs)
takes `double delta`, accumulates `row.Xp + delta` as `double`, and binds it with
`AddWithValue("$xp", xp)` into `xp INTEGER NOT NULL DEFAULT 0`
([RpgStore.cs:397](../../../src/FusionRpg.Data/Sqlite/RpgStore.cs)). Latent only because the single
caller passes a `long` ([ExpeditionEndpoints.cs:137](../../../src/FusionRpg.Server/ExpeditionEndpoints.cs)).
This is exactly the shape §4.1 of the 2026-09-04 audit fixed for player XP, in the sibling table that
audit did not look at.

### P3. Soul earns are `int` on a path `contentScale` multiplies ⚠

**FACT.** [SoulEarnPolicy.cs:74,79](../../../src/FusionRpg.Core/Demons/SoulEarnPolicy.cs) return `int`,
through [ContentScale.cs:24-35](../../../src/FusionRpg.Core/Power/ContentScale.cs), whose signature is
`Apply(int rolledValue, long contentScaleMilli) → int` with `checked((int)…)`. The balance is `long`
([RpgStore.cs:508-516](../../../src/FusionRpg.Data/Sqlite/RpgStore.cs), all `INTEGER`).

CLAUDE.md's row 1 is *"`long` for any magnitude `contentScale` can touch"*, and its table puts `int`
whole units at Θ=103,557. It **throws rather than wraps**, so this is a width finding, not a
corruption one — but `ContentScale.Apply` is the single funnel every scaled magnitude passes through,
which makes it the widest-blast-radius narrowing in the repo.

`scripts/audit-overflow.py` reports **0 critical, A3=34, A7=23** this session, and does not flag
`ContentScale.Apply` because the cast is `checked`.

### P4. What is stored is effort, not derived power — this half is right ✅

Checked deliberately, because storing derived power makes a rebalance unmigrable:

- `rpg_actor_progression` stores `level` and `xp`, never `P(Θ)` ([RpgStore.cs:355-368](../../../src/FusionRpg.Data/Sqlite/RpgStore.cs)).
- `rpg_aptitude_allocation` stores `points` only — *"inputs only"*
  ([RpgStore.Aptitudes.cs:35-43](../../../src/FusionRpg.Data/Sqlite/RpgStore.Aptitudes.cs)).
- `rpg_contract_state` stores `purchased_slots`, never a price ([RpgStore.cs:486-492](../../../src/FusionRpg.Data/Sqlite/RpgStore.cs)).
- Action unlocks carry `EarnCount` and recompute the rung on every read
  ([UnlockState.cs:59](../../../src/FusionRpg.Core/Actions/Unlock/UnlockState.cs)) — though nothing
  persists them yet at all: `FromPersisted` at `:72` has no store behind it.

**Every ladder stores its effort and derives its power.** Retuning `bMilli` therefore needs no data
migration — the §4.3 pin's promise, holding in the schema and not only in the document.

---

## 5. §10 inventory integrity

**Row count, by counting.** §10.1 carries rows 1–6; §10.2 carries 7–16, 18, 19 (17 retired). **18
rows.** The machine-readable mirror `docs/architecture/power/inventory.json` carries **20** — ids 1–16
plus 20, 21, 22, 23. **FACT**, both counted this session.

They have diverged in both directions:

| Problem | Detail |
|---|---|
| **Rows 18 and 19 are missing from `inventory.json`** | `thetaOffset` (added to §10.2 on 2026-09-01) and the action unlock ladder (2026-09-03) never reached the mirror. The file's own `_meta.rebalance` says *"Adding a row is a reviewed change to ssot-power-scale.md §10 first, this file second"* — the second half did not happen, twice |
| **Rows 20–23 exist only in the mirror** | `PowerLadder`, `ChannelLadder`, `ContentScale`, and a pointer to §10.3. Legitimate entries, but §10 is the authority and does not list them |
| **Two `location` values no longer resolve** | Row 3 → `Stats/Derived/IProgressionPowerProvider.cs` and row 4 → `Progression/RpgXpPowerScale.cs`. Both files are **deleted** (`find` returns nothing). The verdicts say "Deleted", so the semantics are right and only the addresses are dead |
| **`power-map.md`'s inventory line is stale** | *"the SSOT's §10 sweep found **14** power-shaped scales; 6 collapse into `Θ`, 8 are bounded or relative."* It is 6 + 12 = 18 |
| **§11.2 says enhancement is unbuilt** | *"Both features are unbuilt. Enhancement (lane I6) and rarity promotion (lane I1) are specs, not code."* Enhancement **is** code — below |

**Ladders found in code with no §10 row:**

1. **`EnhancePolicy.GainMicro` / `LinearGainMilli`** — and the repo's own guard already says so. Run
   this session:

   ```
   > pwsh -File scripts/guard-power.ps1        (exit 1)
   POWER GUARD FAILED:
     G2 src\FusionRpg.Core\Items\Mutation\EnhancePolicy.cs:85: private f(level)-shaped method outside Core/Power
     G3 src\FusionRpg.Core\Items\Mutation\EnhancePolicy.cs:85: power-shaped method not listed in inventory.json
     G2 src\FusionRpg.Core\Items\Mutation\EnhancePolicy.cs:115: private f(level)-shaped method outside Core/Power
     G3 src\FusionRpg.Core\Items\Mutation\EnhancePolicy.cs:115: power-shaped method not listed in inventory.json
   ```

   **`EnhancePolicy.cs` is untracked** (`git status` → `??`), so this is work in flight, not a
   regression on `main`. The guard is doing exactly its job: a new asymptotic reward ladder appeared
   and the closed inventory was not amended. It needs a §10 row *and* an `inventory.json` row before
   the module lands, and §11.2's "unbuilt" line needs correcting with it.

2. **`DropVolume.VolumeScaleMilli`** — `base + slope·(Θ − pin)`, a private linear read of `Θ`
   ([DropVolume.cs:35-42](../../../src/FusionRpg.Core/Items/Drops/DropVolume.cs)). Its own comment
   argues the exemption (*"a drop count is neither — it is a rate — so it reads Θ, which is the same
   axis, not a private curve"*), and the argument is sound. But §10 is a **closed list**, and an
   exemption argued in a source comment is what evidence rule 2 says is not evidence. It passes the
   guard only because `thetaActor` is not spelled `level`.

3. **Specimen level's flat-100 ladder** (M1) — a level curve with no row and no register entry,
   invisible to the guard because it lives in `FusionRpg.Data`, whose store files the `f(level)`
   heuristic does not match.

---

## 6. Already consistent — checked, and found correct

Named so the next sweep does not re-litigate them.

- **The main line is exactly linear in effort.** Row 1's algebra reproduces §10.5's claim from the
  shipped constants rather than from the document: `0.107` power per kill at level 20 and at level
  2,000.
- **`P(Θ)` is integer-exact and throws.** `TriangularMilli` halves the even factor **before**
  multiplying ([PowerLadder.cs:49-55](../../../src/FusionRpg.Core/Power/PowerLadder.cs)); `MaxIndex` is
  computed from the loaded curve rather than fixed; `PowerIndexOverflow` refuses to wrap.
- **Contests still read `Θ`, magnitudes still read `P(Θ)`.** `BaseAccuracy/Dodge/CritRate/CritResist`
  are linear in `Θ` and deliberately not routed through the ladder
  ([BattleModels.cs:176-190](../../../src/FusionRpg.Core/Battle/BattleModels.cs)); `AptitudeReadFunctions`
  carries both modes with the reason written next to each
  ([AptitudeReadFunctions.cs:31-48](../../../src/FusionRpg.Core/Stats/Aptitudes/AptitudeReadFunctions.cs)).
  PS-3 holds at every call site checked.
- **`Wf = Wa` is enforced, not merely documented.** `PowerIndexComposer.ValidateWeights` throws
  `PowerWeightInvalid` ([PowerIndexComposer.cs:47-51](../../../src/FusionRpg.Core/Power/PowerIndexComposer.cs)),
  and `power-scale.v2.json` ships `WfMilli = WaMilli = 25000` — audit F8's divergence cannot recur.
- **`Wm: null` fails loudly** — `PowerWeightMissing` at
  [PowerIndexComposer.cs:67-68](../../../src/FusionRpg.Core/Power/PowerIndexComposer.cs).
- **The pin holds.** `guard-power.ps1`'s G4 checks `P(pinIndex) == pinValue` for every version on
  disk, and passed this session; the only failures were G2/G3 on `EnhancePolicy`.
- **`long` end to end on the player XP path** — curve, awards, `RpgActorState.Xp`, and the loader
  rejects a fractional tuning value rather than truncating it
  ([ProgressionTuning.cs:91](../../../src/FusionRpg.Core/Progression/ProgressionTuning.cs)).
- **Loam is `Θ`-invariant on both legs**, as §10.4 decided — yield `6`/level, upkeep `5`/level, both
  linear, net flat. PS-5 satisfied by "neither."
- **No power constant lives in code.** G1 passed; `power-scale.v2.json` holds the curve and every
  weight, and `A` is derived at load.
- **Overflow audit: 0 critical.** `A3=34`, `A7=23`, none blocking; §10.7's decision that `double`
  stands in stat composition is unchallenged by anything found here.

**One honest gap, stated rather than hidden.** `Θ_actor` is hydrated from `daveLevel` alone — both the
server and the injector pass `RealmsAdvanced: 0, PvzRuns: 0`
([ServerPowerIndexProvider.cs:41-50](../../../src/FusionRpg.Server/Power/ServerPowerIndexProvider.cs),
[RpgClient.cs:445-446](../../../src/FusionRpg.Injector/RpgClient.cs)). Of §5's five ladders, one
carries data. That is documented at `ServerPowerIndexProvider.cs:12-18` as an unbuilt feature rather
than a wiring gap, and it is not a defect — but it does mean PS-6's axis-share measurement has nothing
to measure yet, and §5.1's "both sides climb together" property is currently vacuous.

---

## 7. The reconciliation, ordered by (damage if left) ÷ (cost to fix)

| # | Fix | Damage if left | Cost | Kind |
|---|---|---|---|---|
| **1** | **Specimen levels read `RpgXpCurve`** — add a `specimen` row to `progression.v1.json`'s `xpCurve` and replace the `while (xp >= 100.0)` loop with the shared drain. Types go `long` in the same edit (P2) | A live ladder whose reward-per-effort rises linearly forever: 225× cheaper than a player level at level 1,000, and widening by construction | One store method, one tuning row. Curve, loader and `checked` arithmetic already exist | **real defect** |
| **2** | **Decide soul sinks before `Θ_enemy` becomes real.** Either the sinks take `contentScale` (PS-5's "same read") or the faucet drops it. Write the decision into §10.4 beside the sentence whose premise it is | Inflation with a delay fuse — the mirror of §11.7's starvation. At Θ=500 a clean win pays ~130 summon pulls | A decision, plus either three multiply sites or one deletion. **Free today**, because `VanillaPvzKillAndRunTheta = 20` makes both options byte-identical | **real defect, latent** |
| **3** | **State the XP/`contentScale` rule in §10.5 before anyone wires `NoKillPowerScaleYet`:** *"if the XP faucet takes a `P(Θ)` read, `XpToNext` takes the same read, or neither does"* | Levels go exponential in effort — the §6.2 failure mode, with a green test on top of it because the identity value is 1.0 | One sentence, plus a test asserting `NoKillPowerScaleYet == 1.0` and saying why | **prevention** |
| **4** | **Give `EnhancePolicy` its §10 row and its `inventory.json` row**, and correct §11.2's "both features are unbuilt." Verdict: bounded, item-relative, `Θ`-free by design — the same class as row 7 | The guard is red now, and a red guard that is normal stops being read | Two rows and one sentence. The guard already found it | **real defect (doc)** |
| **5** | **Resync `inventory.json` to §10.** Add rows 18 and 19; promote 20–23 into §10 or drop them; repoint rows 3 and 4 (or retire them the way row 17 is); fix `power-map.md`'s "14" | G3 checks against a list that is not the SSOT. That is how a fourteenth curve got in last time | One file, one paragraph | **real defect (doc)** |
| **6** | **Migrate `rpg_xp_ledger.xp_before` / `xp_after` to `INTEGER`** using the `ReadXp` tolerance shim the snapshot migration already established, and widen the four `double` DTO fields | A persisted `double` XP path — the exact rule CLAUDE.md names. Harmless until XP gains a multiplier, which is what fix 3 is about | Two columns, one reader, four DTO fields; the migration pattern is already written | **real defect** |
| **7** | **Pick one index for the passive tree's gate quantities** (§3, option 1 preferred — it falls out of fix 1). Name explicitly that a tier gate makes an aptitude point absolutely valuable for the first time | D26's `req(t)` is exact on one axis and meaningless on the other three | A decision. Two of the four counters do not exist yet, so it is cheapest **now** | **decision owed** |
| **8** | **Pair the star-merge reward to its triangular cost** — `perStar · star(star+1)/2`, D26's own fix — and reconcile `promotionCost` (flat) with `recipeCost` (escalating) | Star 5 is a 2× worse deal than star 1; two prices for one rung | Two tuning values and one formula | **real defect, bounded** |
| **9** | **Widen `ContentScale.Apply` to `long`** | The single funnel every scaled magnitude passes through is `int`-bounded. It throws rather than wraps, so this is width, not corruption | One signature and its callers | **hygiene** |
| **10** | **Give `DropVolume` a §10 row**, recording its exemption in the SSOT rather than in a source comment | An exemption argued only in a comment is not evidence | One row | **hygiene** |

**Not defects — documented exceptions, listed so they are not "fixed":** the XP cost ladder (§10 row
6, exempt by name); the affix tier ladder and the ilvl gate (rows 7 and 14, bounded and level-free);
loam (§10.4, `Θ`-invariant by decision); loyalty and every per-mille track (§11.6, bounded ratios); the
action rung cap (§11.2, a soft content window); the contest reads' linearity in `Θ` (PS-3); and
`PowerVector` staying scale-free (§1).

---

## 8. Design-gate checklist

```
[x] Subsystems identified: power, progression, economy/souls, demons/contracts/fusion,
    items/enhancement, aptitudes, actions, world/loam, data/persistence.
[x] Read this session, in full: DESIGN-GATE.md; power/ssot-power-scale.md (all of it, §10 and
    §11 included); power-map.md; tunables-ssot.md §1-3; passive-tree-ideal.md §3.5, §4, §5, §11;
    research/progression-shape-audit-2026-09-04.md.
[x] Every factual claim cites file:line.
[x] Verified against CODE, not comments — PowerLadder.cs, ContentScale.cs, PowerIndexComposer.cs,
    ChannelLadder.cs, RpgProgression.cs, RpgXpAwardMap.cs, SoulEarnPolicy.cs, ContractPolicy.cs,
    StarPolicy.cs, EnhancePolicy.cs, AptitudeReadFunctions.cs, PointBudget.cs, DropVolume.cs,
    UnlockLadder.cs, BattleModels.cs, WebMatchService.cs, RpgStore.cs schema, RpgStore.Souls.cs,
    RpgStore.UniqueActors.cs, ServerPowerIndexProvider.cs, inventory.json.
[x] Counts verified by counting: §10 rows (18) and inventory.json rows (20), not read off prose.
[x] Constraints TESTED, not assumed: ran scripts/guard-power.ps1 (exit 1, four G2/G3 failures on
    EnhancePolicy.cs) and scripts/audit-overflow.py (0 critical, A3=34, A7=23).
[x] Read the surrounding section of every rule quoted — PS-3, PS-5, PS-7, PS-8, §10.4, §10.5,
    §11.2, §11.6, §11.7, §11.7a.
[x] Nothing contradicts a §2 invariant. M2 and M3 are PS-5 restated, not challenged.
[ ] Corrections NOT yet propagated — this is a research file. Fixes 4, 5 and the §11.2 correction
    are edits to ssot-power-scale.md, inventory.json and power-map.md that this audit does not make.
```

---

## Fix log — 2026-09-05

Owner instruction: work the whole ordered list. Eight of ten landed; two are held for an owner call.

| # | Fix | State |
|---|---|---|
| 1 | **Specimen levels read the shared XP curve.** Was a hardcoded flat `100.0` loop feeding the same quadratic `P(Θ)` as the player line, making specimen power quadratic *in effort*. Now `RpgActorKinds.Specimen` on `RpgXpCurve`, with its own `xpCurve.specimen` tuning row. `first=100` keeps level 1 byte-identical; only the late-game divergence moves | ✅ done |
| 2 | **Soul sinks paired to the faucet.** New `SoulSinkPolicy` applies the same `ContentScale` the faucet uses; `NextSlotPrice`/`RitualPrice` now require `(thetaContent, tuning)`. One shared `VanillaPvzTheta` constant so faucet and sink cannot drift. Byte-identical today (`contentScale(20) = 1.000`), which is exactly why it was worth doing now | ✅ done |
| 3 | XP/contentScale rule written into §10.5 as **PS-5x** | ✅ done |
| 4 | `EnhancePolicy` §10 row + §11.2's stale "unbuilt" | ✅ done — and the audit was **wrong** that the file is untracked and the guard red: it is tracked, and `guard-power.ps1` exits 0 |
| 5 | `inventory.json` resynced to §10 (both now **27 rows**), stale paths repointed, `power-map.md`'s "14" corrected | ✅ done |
| 6 | **XP ledger migrated.** `xp_before`/`xp_after` REAL → INTEGER; three reads moved to the storage-class-tolerant `ReadXp`, so legacy databases keep working; four DTO fields widened to `long` | ✅ done |
| 7 | One index for the tier gate — **half-closed** by fix 1. Specimen levels and aptitude points now share a shape; `element_mastery` and almanac XP still do not exist | ◐ partial |
| 8 | Star-merge reward pairing · promotion-vs-recipe pricing | ✅ done — owner chose **10 stars + hold the cap**, see below |
| 9 | `ContentScale.Apply` widened to `long` in and out; `SoulEarnPolicy.KillEarn`/`MatchEndEarn` follow | ✅ done |
| 10 | `DropVolume` §10 row | ✅ done |

**One correction to this document.** `XpReasonBucket.Sum` stays `double` deliberately, and that is not
a missed defect: it is a rebuildable per-reason **cache** serialized as JSON in a payload column, and
live rows already hold `12.0`, which a `long` property would refuse to deserialize. The SSOT value it
summarizes — `rpg_xp_ledger.delta` — is `long` end to end. The comment now says so in place.

### Fix 8, as decided (owner, 2026-09-05)

Two halves, both landed.

**Star merge — the reward is now indexed on the cost, and the ladder runs to 10.**

The defect: cumulative sacrifices to star `n` are `C(n) = n(n+3)/2` (triangular) while the reward was
`perStar × n` (linear), so reward-per-sacrifice was `60/(n+3)` — star 5 was an **exactly 2× worse deal**
than star 1. Same shape as the passive tree's tier-ladder defect, and the same fix.

`StarPolicy.StarPowerMilli(n) = perStar · n(n+3) / (ReferenceStar + 3)`, anchored at star 5 so **no
demon at or below the old cap changes value**. That divisor is derived from the anchor, not tuned.

| star | 1 | 2 | 3 | 4 | **5** | 6 | 7 | 8 | 9 | 10 |
|---|---|---|---|---|---|---|---|---|---|---|
| reward (‰) | 15 | 38 | 68 | 105 | **150** | 203 | 263 | 330 | 405 | 488 |
| cumulative sacrifices | 2 | 5 | 9 | 14 | **20** | 27 | 35 | 44 | 54 | 65 |
| **reward per sacrifice** | 7.5 | 7.6 | 7.56 | 7.5 | **7.5** | 7.52 | 7.51 | 7.5 | 7.5 | 7.51 |

Flat to within integer rounding, against a 2× spread before. `MaxStar` 5 → 10 and per-rarity caps
doubled (3/4/5 → 6/8/10), preserving the rarity shape exactly. `SacrificesForStar`'s `n+1` curve — a
2026-08-21 owner lock — is **untouched**; the reward moved onto it instead.

`StarPolicyTests.Star_reward_is_paired_to_the_triangular_sacrifice_cost` pins the property by
*computing* reward-per-sacrifice across the whole ladder rather than asserting copied numbers, so a
future tuning change that breaks flatness fails loudly.

**Promotion — per-rung pricing.** `promotionCostByRarity` replaces the flat 200 souls, shaped like
`recipeCost`'s own 150 → 1000 escalation (Chaff 150 … Firstseed+ 1000). Promotion is once per
specimen, so it cannot compound. The flat `promotionCost` row stays as the fallback for a rung the
table does not name.
