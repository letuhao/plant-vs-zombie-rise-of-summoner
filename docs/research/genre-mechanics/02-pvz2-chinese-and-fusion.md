# The Chinese PvZ2 line and PvZ Fusion — the RPG-ised branch of the franchise

Research note. Evidence only — no proposals, no design.

## The finding in one paragraph

**The Chinese branch of Plants vs. Zombies solved "a tower defence game has no long-term progression" three
different times, and each answer is a different shape.** Chinese PvZ2 (植物大战僵尸2, PopCap Shanghai then
Talkweb/拓维) bolted a flat, per-unit RPG ladder onto the lawn: every plant has five tiers (阶) bought with
plant-specific fragments (碎片) and coins, and each tier is a plain multiplier — 150% / 200% / 250% / 300% of
base attack and HP — plus one authored skill unlock. PvZ Online (Tencent, 2013–2018) went further and gave
plants stars, awakening, a greenhouse, a nutrition room and a laboratory, i.e. a real gear-and-stat layer.
**PVZ Fusion (植物大战僵尸融合版 / PVZRH, by the Bilibili creator 蓝飘飘fly) — the host game of this project —
took the opposite route and put the whole progression inside the match.** It has no plant levels worth the
name; instead it ships **697 distinct plant types and 228 zombie types in 3.9** (computed from the shipped
`Assembly-CSharp.dll`), a recursive pair-fusion tree with an explicit depth statistic, a six-rung card rarity
enum (`White/Green/Blue/Purple/Gold/Red`), and a roguelite Travel mode carrying **177 advanced buffs, 56
ultimate buffs, 42 investment buffs and 142 zombie debuffs** as drafted traits. The Chinese-market lesson that
repeats across all three: the *content* is the ladder, the numbers are the smallest part of it, and difficulty
in the best of them (Fusion) scales enemy **volume and damage reduction**, not enemy HP.

---

## 0. Evidence tiers used in this file

| Tier | What it is | How it is marked |
|---|---|---|
| **A — shipped data** | The game binaries on this machine, read with Mono.Cecil (no execution). PVZ Fusion 3.8.1 and 3.9. | "binary dump", plus `(computed)` on any number I tallied |
| **B — community wiki** | BWIKI (`wiki.biligame.com/pvzrh`, `/pvzz`), `plantsvszombies.wiki.gg`, Moegirl. Player-maintained. | **second-tier** |
| **C — portal guides** | Chinese game-portal walkthroughs and forum posts. Numbers here are player-measured. | **third-tier**, sample size noted where given |

There is no tier-A source for the Chinese PvZ2 line — I do not have those binaries. Everything in §1, §2, §4,
§5, §6 is tier B or C, and is marked.

Local first-party files used for tier A:

- `H:\Games\PVZ-Fusion-3.9_MelonLoader\MelonLoader\Il2CppAssemblies\Assembly-CSharp.dll` (PVZ Fusion 3.9)
- `H:\Games\PVZ FUSION 3.8.1 FULL MOD TOOL\BepInEx\interop\Assembly-CSharp.dll` (PVZ Fusion 3.8.1)
- Game identity `PlantsVsZombiesRH_Data/app.info` → Company `LanPiaoPiao`, Product `PlantsVsZombiesRH`
  (already recorded in [`docs/research/sources.md`](../sources.md))

---

## 1. Chinese PvZ2 plant progression — tiers, not levels

### 1.1 The two ladders are genuinely different systems

**FACT (tier B/C).** The international PvZ2 and the Chinese PvZ2 do not share a progression system. They share
almost nothing but the art.

| | International PvZ2 | Chinese PvZ2 (中文版) |
|---|---|---|
| Unit progression | **Seed-packet levels**, 1 → 10 → 15 → 20, capping around 200 in later builds | **Tiers (阶)**, 1 → 5 |
| What a step buys | Cooldown reduction, sun-cost reduction, ability strength | **A flat attack + HP multiplier, plus one authored skill** |
| Currency | Seed packets from the level loop | **Plant-specific fragments (碎片) + coins (金币) + coloured culture fluid (培养液)** |
| Costumes (装扮) | **Cosmetic only** | **Carry stats and ability upgrades — effectively mandatory** |
| Feel | "weak early, transformative late" (低等级看不出多少，高等级质变) | Front-loaded: tier 2 already doubles nothing but multiplies everything by 1.5 |

Source: [TapTap — 中文版和国际版的不同点](https://www.taptap.cn/moment/427773268418626266) (tier C, player-written
comparison). Corroborated on the international-level side by
[Moegirl — 植物大战僵尸2/中文版植物强化](https://zh.moegirl.org.cn/植物大战僵尸2/中文版植物强化) (tier B; see
"What I could not find" — Moegirl returned HTTP 403 to direct fetches and I could only read it through search
indexes).

### 1.2 The tier ladder, with real costs

**FACT (tier C).** Five tiers. A newly acquired plant is tier 1.

| Step | Fragments | Coins | Other | Resulting ATK/HP vs base |
|---|---|---|---|---|
| Acquire (→ 一阶) | 10 | — | — | 100% |
| 一阶 → 二阶 | 30 | 50,000 | — | **150%** |
| 二阶 → 三阶 | 50 | 100,000 | — | **200%** |
| 三阶 → 四阶 | 50 | 200,000 | — | **250%** |
| 四阶 → 五阶 | 80 | 500,000 | **10 bottles of matching-colour culture fluid (培养液)** | **300%** |
| **Total to tier 5** | **220 (computed)** | **850,000 (computed)** | 10 bottles | — |

Costs: [7723 — 全植物升五阶优先级](https://www.7723.cn/strategy/27709.html) (tier C) for the tier-5 row and the
acquisition-fragment table; the tier-2/3 rows are independently confirmed by
[Zhihu repost of the PvZ2 中文版平民常识 guide](https://zhuanlan.zhihu.com/p/537996376) (tier C) — "二阶30碎片，
5W金币，三阶50碎片，10W金币".

Multipliers: taken verbatim from a per-plant tier table on
[plantsvszombies.wiki.gg — PvZ2C:向日葵 (Sunflower)](https://plantsvszombies.wiki.gg/zh/wiki/PvZ2C:向日葵)
(tier B): 二阶 战斗训练 "攻击力和生命值提升至最初的150%" → 三阶 细胞活化 200% → 四阶 战斗能力 250% → 五阶 300%.

**Internal corroboration (computed).** The 7723 guide separately says that acquiring a plant you already own at
tiers 1–5 refunds **10 / 40 / 90 / 140 / 220 fragments**. Those are exactly the cumulative fragment costs of
reaching each tier (10, 10+30, 10+30+50, +50, +80). The two independent tables agree, which is the strongest
consistency check available without the binaries.

**FACT (tier B).** Each tier also unlocks one authored ability, not just the multiplier. Sunflower's ladder:
tier 2 "回收阳光" — drops 25 sun on death; tier 3 "回收强化" — that drop becomes 75 sun; tier 4 "能力觉醒" —
high chance to fire its Plant Food ultimate the moment it is planted; tier 5 "爆炸瓜子" — explosive seeds. Same
source as the multiplier table.

Note on terminology: **觉醒 ("awakening") in Chinese PvZ2 is the name of an individual tier-4 skill, not a
system.** The system called 觉醒 belongs to PvZ Online — see §7.1.

### 1.3 Player-measured tier effects on abilities

**FACT (tier C, small sample — the author states 1,000 trials and invites correction).**
[GamerSky — 植物进阶效果测试一览](https://www.gamersky.com/handbook/201407/387967.shtml):

| Plant | Tier 2 | Tier 3 |
|---|---|---|
| Potato Mine | 8 s arm time | **Instant arm** |
| Squash | 20% chance not to be consumed | **50% chance** |
| Peashooter | 6.5% chance of a quad shot | **15% chance** |
| Fire Dragon | 50% blue flame | **100% blue flame** |

**The shape is the point: the multiplier is linear, the ability rider is a probability that roughly doubles per
tier.** That is a deliberate split — the smooth part keeps every tier feeling like it did something, the
probability part is where the "質變" (qualitative change) moments live.

### 1.4 Quality, families and gene editing — the meta layer above the tier ladder

**FACT (tier B, Moegirl via search index).** Chinese PvZ2 stacks three more systems on top of tiers.

| System | Rule | Numbers |
|---|---|---|
| **品质 (quality)** | Per-plant rarity colour, fixed at acquisition | 5 colours: white 白 / green 绿 / blue 蓝 / purple 紫 / orange 橙 (also written 金) |
| **家族 (family)** | Plants are grouped into families; family level gates the *upper bound* of a randomly rolled talent attribute | **51 families total: 1 green, 4 blue, 5 purple, 41 orange** |
| **家族等级 (family level)** | Sum over owned plants: white +1, green +2, blue +3, purple +4, orange +5; **each tier on each plant +1; each costume +1** | Free reroll of family attributes **3× per day** |
| **基因编辑 (gene editing)** | Added in Chinese version **3.0.3**. Unlock a gene sequence for a specific plant; duplicate sequences convert to gene essence; **1 essence = 1 level, max level 10** | Max 10 |

Sources: [Moegirl — 中文版植物强化](https://zh.moegirl.org.cn/植物大战僵尸2/中文版植物强化) (tier B, read via
search index only). Plant-colour distribution among the 116 tier-5-capable plants:
**white 4, green 4, blue 15, purple 28, gold 65** — [7723](https://www.7723.cn/strategy/27709.html) (tier C).

**INFERENCE (mine).** The family system is the interesting one, and it is not a stat system — it is a
*collection* system wearing stat clothes. Family level counts plants owned, tiers bought and costumes bought,
then converts that total into a better random roll. Every purchase in the game feeds it. That is why it exists:
it makes buying the 41st orange plant matter even when you will never field it.

### 1.5 What each of these costs and breaks

| Mechanic | Problem it solves for the player | What it costs the designer | What breaks when tuned wrong |
|---|---|---|---|
| **Tier multiplier (150→300%)** | "My favourite plant stopped working three worlds ago." A tier restores it without a new unit. | Every level's zombie table now has to be beatable at tier 1 *and* not trivial at tier 5 — a 3× spread on player output per unit. | Too steep and the game splits into "tiered plants" and "dead plants" — the intl-vs-CN complaint that high-tier fragments are randomly gated and low-tier plants get crushed ([BWIKI pvzz](https://wiki.biligame.com/pvzz/植物大战僵尸2中文版), tier B). Too shallow and the fragment grind has no payoff. |
| **Fragment gating (plant-specific)** | Gives every drop a name. You are never "farming currency", you are farming *that plant*. | Needs a per-plant drop source, a duplicate-conversion path, and a shop; otherwise a plant you cannot roll is a plant you cannot use. | The failure mode is documented: fragments for high-tier plants are "获取难度大、随机性强" — hard to get and highly random. Players stall on one unit and stop playing. |
| **Authored per-tier skill** | Gives the tier a memory. Nobody remembers +50% ATK; everybody remembers "potato mine arms instantly". | One design pass per plant per tier — 116 × 4 ≈ 464 authored riders at minimum. This is the single most expensive item in this file. | Skip it and tiers feel like a tax. Overdo it and tier 3 becomes mandatory for a whole archetype. |
| **Family level from ownership** | Makes collecting pay off even for units you never field. | A second economy that must not outrun the first. | If family attributes outweigh tiers, the optimal play becomes "buy breadth, never deepen" — which kills the fragment loop it was meant to support. |
| **Gene editing (max 10)** | A per-plant sink for players who have already maxed tiers. | Yet another currency with its own drop table. | It is the third ladder on the same axis. Three ladders means no ladder is legible. |

---

## 2. Fusion / combination in the Chinese PvZ2 line

**FACT (tier B).** Searching the Chinese PvZ2 wikis for 融合 / 合成 (fusion / combination) returns nothing.
`plantsvszombies.wiki.gg/zh` returns "没有匹配此查询的结果" for `PvZ2C 融合`.

**The Chinese PvZ2 has no two-plants-become-one mechanic.** What it has instead:

| Mechanism | What it actually is |
|---|---|
| **Upgrade plants (升级植物)** — Twin Sunflower, Gatling Pea etc. | Inherited from PvZ1: plant a specific plant *on top of* another specific plant. One-to-one, authored, a fixed short list. Not a combinatorial system. |
| **Imitater (模仿者)** | Copies one chosen seed packet into a second slot. A deck-building convenience, not a fusion. |
| **Plant Food / 能量豆** | Single-use ability trigger, not a combination. |
| **Costumes (装扮)** | Additive stat/ability layer on one plant. |

**INFERENCE (mine).** This matters for framing: **the fusion idea did not come down the official Chinese line —
it came from the fan-mod line.** PVZ Fusion is not a continuation of a Talkweb feature; it is an independent
invention by a Bilibili creator, and it is the only member of this family where combination is the core loop.
Nothing in the official Chinese roadmap has to be read as prior art for it.

---

## 3. PVZ Fusion (植物大战僵尸融合版 / PVZRH) — the host game

This is the priority section, and it is the only one with tier-A evidence.

### 3.1 Identity

**FACT.** Fan-made, by the Bilibili creator **蓝飘飘fly (LanPiaoPiao)**. Unity IL2CPP. Local `app.info` reports
Company `LanPiaoPiao`, Product `PlantsVsZombiesRH`. Unity 2022.3.62f1c1 on the 3.8.1 pack
([`docs/research/sources.md`](../sources.md), tier A). Wiki: `wiki.biligame.com/pvzrh` (BWIKI, tier B), which
reports ~10,003 pages and ~93,246 edits — a large, actively maintained community.

### 3.2 Content scale — shipped binary counts

**FACT (tier A, computed by enumerating static enum fields in `Assembly-CSharp.dll`).**

| Enum | 3.8.1 | 3.9 | Delta |
|---|---|---|---|
| `PlantType` | 677 | **697** | **+20** |
| `ZombieType` | 225 | **228** | +3 |
| `BulletType` | 240 | **244** | +4 |
| `SceneType` (board backdrops) | 43 | **44** | +1 |
| `LevelType` | 13 | 13 | 0 |
| `GridItemType` | 12 | 12 | 0 |
| `PetType` | 8 | 8 | 0 |
| `PlantStatus` | 44 | 44 | 0 |
| `ZombieStatus` | 49 | 49 | 0 |
| `PlantDamageAdder` | 57 | 57 | 0 |
| `DamageType` | 19 | 19 | 0 |

> **Counting method — read this before re-counting.** These are named enum members **excluding the
> `Nothing = -1` sentinel** that most of these enums carry. A raw count of static fields therefore
> returns one *more* than the table says (`PlantType` 698 / 678, `LevelType` 14, `GridItemType` 13 …);
> that is agreement, not a contradiction. Re-verified independently 2026-09-02 with Mono.Cecil against
> both interop assemblies.
>
> **Names are not distinct values.** `PlantType` aliases at least one value — `Peashooter = 0` and
> `Ulti_cherryGatling = 0` are the same underlying number. **Use the name count for "how much content
> exists" and never assume the value space is dense or unique.**

**The 3.9 diff is exactly 20 newly named plant types** (computed): `EnderPumpkin, SuperGatlingFume, CoinShroom,
BigCoinShroom, SniperScaredy, SilverHypnoShroom, GoldHypnoShroom, CherryPuff, ChomperScaredy, UmbrellaFume,
DoomPot, DoomUmbrella, SeaChomper, IceStar, FumeChomper, PuffChomper, SeaPot, GarlicStar, UltimatePresentKelp,
UltimateFurnace`.

**That is a clean cross-check against the wiki**, which independently says 3.9 added "16 standard-series plants
and 4 ultimate-series plants" — 20 ([BWIKI 3.9版本](https://wiki.biligame.com/pvzrh/3.9版本), tier B). Two
independent sources, same number. Treat the binary counts in this table as reliable.

Naming-prefix tallies inside 3.9's `PlantType` (computed, prefix match only — indicative, not a taxonomy):
`Ultimate*` **66**, `Super*` **31**, `Ice*` **30**, `Fire*` **18**, `Gold*` **16**, `Big*` **9**.

### 3.3 The fusion system — how it actually works in code

**FACT (tier A).** The fusion tables and lookups live in `Il2Cpp.MixData`, and the tree analysis lives in
`Il2Cpp.PlantMixTreeManager`. Their members, read from the interop assembly:

| Member | What it establishes |
|---|---|
| `MixData.AddRecipe(PlantType a, PlantType b, PlantType result)` | **Recipes are unordered pairs by default.** |
| `MixData.AddOrderedRecipe(PlantType a, PlantType b, PlantType result)` | **There is also an order-sensitive variant** — for some pairs A+B ≠ B+A. |
| `MixData.TryGetMix(PlantType, PlantType, out PlantType, bool)` | Deterministic pair lookup. The trailing `bool` is a second mode flag. |
| `MixData._recipes_random`, `TryGetRandomMix(...)`, `UpdateRandomMix()` | **A second, randomised recipe table that is regenerated at runtime.** Fusion is not always a fixed recipe book. |
| `MixData.TryGetDisMix(PlantType, out ValueTuple<PlantType,PlantType>)` | **Un-fusion exists** — a fused plant can be decomposed back into its two parents. |
| `MixData.BaseOfUltimatePlants` | Ultimate plants are tracked against a declared base set. |
| `MixData.FirstMix / PuffMix / FogPlant / RoofPlant / InitTravel / SpecialPlant / SubInit / HandleSubUlti` | **The recipe book is built in named partitions** — a base pass plus scene- and mode-scoped passes (fog, roof, travel, special, sub-ultimate). |
| `PlantMixTreeManager.PlantMixTrees : Dictionary<PlantType, PlantMixTreeNode>` | Every plant is a node in a fusion tree. |
| `PlantMixTreeManager.ChildToParents : Dictionary<PlantType, List<MixParentInfo>>` | Reverse index; `MixParentInfo` is `{ParentA, ParentB, Result}`. |
| `PlantMixTreeNode.{Depth, IsBasicPlant, DirectChildren, AllDescendants, Recipes}` | **Fusion is explicitly recursive and depth is a first-class property.** |
| `PlantMixTreeManager.MixTreeStatistics.{TotalPlantTypes, TotalMixRecipes, MaxTreeDepth, PlantWithMostChildren, MaxChildrenCount, BasicPlantCount}` | The game itself computes and exposes exactly the roster metrics a generator would want. |
| `PlantMixTreeManager.GetMixPaths(PlantType from, PlantType to) : List<List<PlantType>>` | **There can be more than one route to the same result.** |
| `Il2Cpp.MixedPlant` — `CreateAnim`, `FusionAnimation`, `TakeDamage`, `Crashed` | A transient actor that exists only during the fusion animation. |
| `Plant.DieReason` includes `ByMix`, `ByDisMix`, `ByLevelUp` ([`game-types-381.md`](../game-types-381.md), tier A) | Fusion and un-fusion consume the parents through the normal death path, with their own reason codes. |

**INFERENCE (mine, well-supported).** The fused result is a **distinct authored `PlantType` with its own
authored stats**, not a stat blend of its parents. Evidence: `MixedPlant` carries only animation and
pass-through damage members; the resulting board entity is an ordinary `Plant` whose numbers are written by
`CreatePlant.SetPlantAttributes(Plant)`; and this project's own capture already records `setPlantAttributes` as
a stat source distinct from `start` ([`game-types-381.md`](../game-types-381.md)). I could not find a wiki page
stating whether current HP or buffs carry across a fusion — see "What I could not find".

### 3.4 The fusion rules as the community documents them

**FACT (tier B, [BWIKI 基本玩法](https://wiki.biligame.com/pvzrh/基本玩法)).**

- **Fusion is pairwise and in-match.** You press the fusion button, the cursor enters fusion mode, you click two
  planted plants, and they become one.
- **Fused plants have no seed card of their own.** "融合后的植物本身没有卡牌，只能通过两两融合的方式出现;
  已经融合的植物也可以进一步进行融合" — a fused plant only ever comes into existence by fusing, and it can then
  be fused again. That is the recursion, stated plainly.
- **Recipes unlock with the parent plants by default**; some recipes are gated behind special stages.
- **Fusion priority order (3.2 and later):** planting on empty ground > plant fusion > pumpkin fusion > flower-pot
  fusion. Before 3.2, pumpkin and pot were swapped. **This is a conflict-resolution rule, not a balance rule** —
  it exists because one click can mean four things.
- **Three-plant fusion exists** via 融合洋芋 (Fusion Potato), which merges multiple plants into giant-plant
  cards. The binary corroborates dedicated level types for this: `RandomMix3Level`, `RandomMix4Level`,
  `UltimateRandom4Level` (tier A).
- **副卡 (secondary cards):** you may carry a duplicate of a chosen plant at **double the sun cost**. Purple
  cards, functional white cards (gift boxes, zombie blind boxes, ice boxes), easter-egg cards, and Doom-shroom
  are excluded.

**Triple-fusion recipes, from a third-party mod's own tables** (tier C — a modder's hand-entered data, used only
as corroboration that triples exist and take the form A+B+C, in `study/Magnetar-Client/.../NEFRecipes.cs` in this
repo): e.g. Chomper ×3 → BigChomper; WallNut + TallNut + WallNut → HugeWallNut; Cabbagepult ×3 → CabbageCannon.
**Note the shape: two of the three parents are often the same plant.**

### 3.5 Rarity — two independent axes, both in the binary

**FACT (tier A).** Two enums, and they are not the same thing.

```
Il2Cpp.CardLevel : White=0, Green=1, Blue=2, Purple=3, Gold=4, Red=5
Il2Cpp.UI.Quality : Default=0, silver=1, gold=2, diamond=3, curse=4, iridescent=5, random=6
```

`CardLevel` is the **card's rarity rung**. `Quality` is a separate **upgrade/finish state** applied to an
instance — the gold-coffee-bean upgrade path, plus diamond, curse and iridescent finishes, plus a `random` roll.

**FACT (tier B, [BWIKI 植物图鉴](https://wiki.biligame.com/pvzrh/植物图鉴)).** The wiki's card-background naming
is: 普通品质 white, 稀有品质 green, 超级品质 blue, 弱究极品质 light gold, 强究极品质 deep gold, 奇异品质 red.

**Discrepancy, flagged.** The wiki's six names do not map cleanly onto `CardLevel`'s six values — the wiki has
two golds and no purple, the enum has one gold and a purple. The 副卡 rule separately speaks of "紫卡" (purple
cards) as a real in-game class. **Trust the enum; treat the wiki's colour names as UI descriptions that have
drifted.**

**FACT (tier B).** Rarity is not cosmetic. In Abyss mode, **card background colour determines how many of that
plant you may have planted at once** — rarity is a deck-building budget
([BWIKI search, 深渊模式](https://searchwiki.biligame.com/pvzrh/index.php?search=深渊模式&fulltext=1)).

### 3.6 The plant taxonomy and its counts

**FACT (tier B, per-category BWIKI tables).** The wiki organises plants by 融合度 ("fusion degree"):

| Category | Count | What it is |
|---|---|---|
| 一级植物 (tier 1) | **88** | Base cards you can pick in the seed-selection screen |
| 二级植物 (tier 2) | **270+** | Fused from tier-1 plants; available in all levels |
| 超级植物 (super) | **13** | Need 3–4 tier-1 plants or special materials; unlocked via challenge stages |
| 究极植物 (ultimate) | **103** | Highest fusion degree; largely Travel-mode |
| 巨型植物 (giant) | **8** | Occupy 2 grid cells; made by the giant/triple fusion path |
| 奇异植物 (anomalous) | **10** | 奇异品质 red cards, flagged 限定植物 (limited); versions 2.4 → 3.8.1 |
| 衍生植物 (derivative), 塔防植物 (tower-defence), 武器防具 (weapons & armour) | not counted here | see "What I could not find" |

Sub-total of the counted rows: **≈ 492 (computed)**, against 697 `PlantType` entries in the 3.9 binary. The gap
is derivatives, tower-defence-only plants, weapons/armour, and `Nothing` — consistent, not contradictory.

Category pages: [一级植物](https://wiki.biligame.com/pvzrh/一级植物),
[二级植物](https://wiki.biligame.com/pvzrh/二级植物), [超级植物](https://wiki.biligame.com/pvzrh/超级植物),
[究极植物](https://wiki.biligame.com/pvzrh/究极植物), [巨型植物](https://wiki.biligame.com/pvzrh/巨型植物),
[奇异植物](https://wiki.biligame.com/pvzrh/奇异植物) (all tier B).

**FACT (tier B).** Ultimate plants are Travel-mode-gated: "究极植物只能在旅行模式下解锁配方融合种植" — their
recipes can only be unlocked, fused and planted in Travel mode
([阳光下载站 guide](https://www.tolyg.com/news/6110.html), tier C, corroborated by the BWIKI category page).

### 3.7 Zombies fuse too, and their gear is fusion material

**FACT (tier B, [BWIKI 融合僵尸](https://wiki.biligame.com/pvzrh/融合僵尸)).** Zombies run their own tier chain:
**原型僵尸 (prototype) → 融合僵尸 (fusion) → 精英僵尸 (elite) → 旅行 (travel) → 领袖 (leader) → BOSS**, plus
derivative and upgrade categories. The wiki documents roughly 60+ fusion zombies. Binary: 228 `ZombieType`
entries in 3.9 (tier A, computed).

**FACT (tier B, [BWIKI 武器防具](https://wiki.biligame.com/pvzrh/武器防具)).** Zombies carry weapons and armour
as separate objects with their own rules:

- **Armour class 1** extends the zombie's health pool against most damage types.
- **Armour class 2** blocks left-to-right projectiles and stops fire-pea splash.
- **Armour class 3** blocks lobbed projectiles and stops splash.
- **Special armour** counts toward the zombie's toughness; losing it *reduces the zombie's stats*.
- Every item has a **magnetism rating**: strong (any magnet-shroom), weak (only Magnet-shroom King), extremely
  weak (only Magnetic Pumpkin variants), or non-magnetic.
- **Magnet plants consume stolen armour** to spawn weakened copies of the corresponding zombie or to buff
  themselves.

The binary corroborates the armour taxonomy: `Zombie.FirstArmorType` = `Nothing, Cone, Bucket, Doll,
FootballHelmet, WallNut, TallNut, TallNutFootball, BucketNut, Balloon, IronBalloon` (tier A), and the capture
schema already tracks `theFirstArmorHealth` / `theSecondArmorHealth`
([`game-types-381.md`](../game-types-381.md), tier A).

**This closes a loop most tower defences never close: enemy equipment is a player resource.** The wiki's plant
taxonomy even names a "metal plants" class defined as "fusion materials that include iron gear dropped by
zombies".

### 3.8 Difficulty scaling — volume and damage reduction, not HP

**FACT (tier B, [BWIKI 机制/难度与出怪](https://wiki.biligame.com/pvzrh/机制/难度与出怪)).** Seven difficulty
rungs, **N0–N6**, default **N2**. N6 exists only inside skin challenges.

| Rung | Name | Spawn count vs vanilla | Zombie speed | Zombie damage reduction | Other |
|---|---|---|---|---|---|
| N0 | 简单 (Easy) | ×1 | 1.0 | — | Plant HP ×2 |
| N1 | 普通 | ×3 | — | — | — |
| N2 | 正常 (default) | ×5 | — | — | — |
| N3 | 困难 | ×7 | — | — | Elite zombies suppressed before flag 1 |
| N4 | 极难 | ×10 | **+0.1** | **30%** | Ash damage stops ignoring armour class |
| N5 | 你确定？ | ×10 | **+0.2** | **60%** | Nut plants stop preventing splash; bosses from wave 4 |
| N6 | 皮肤挑战 | ×10 | +0.2 | 60% | **Max toughness +40%**, first zombie at **3 s**, wave interval capped at **10 s** |

Normal-zombie speed multiplier band overall: **1.0 – 1.4** (tier B, Moegirl via search index).

**The headline finding: no rung on this ladder multiplies zombie HP.** It multiplies how many zombies arrive,
how fast they walk, and how much of your damage they discard. HP inflation is reserved for the roguelite modes
(§3.10).

### 3.9 Modes — thirteen level types in the binary

**FACT (tier A).** `Il2Cpp.LevelType`:

```
Nothing=-1, Advanture=0, Challenge=1, IZ=2, Survival=3, Explore=4, TravelAdvanture=5,
SkinLevel=6, AbyssRealm=7, NewAdvanture=8, TowerLevel=9, StarAdvanture=10, CustomLevel=11
```

`IZ` is I-Zombie (play as the zombies). `Survival` is 无尽 (endless). `AbyssRealm` is 深渊. `TowerLevel` is the
tower-defence variant. `StarAdvanture` is 星辉冒险 (added 3.3). `CustomLevel` is the player level editor, which
by 3.5 has online upload/download (`GameLevel.OnLine.*` types in the binary).

The binary also carries one-off level classes that are effectively mini-games:
`RandomMix3Level`, `RandomMix4Level`, `UltimateRandom4Level`, `MinesweeperLevelData`, `TreasureFateLevel`,
`NumBattleMecLevel`, `AutoChessLevel`, `WheelChallengeLevel`, `TimeTrackLevel`, `NightSnowLevelData`,
`RhythmGameLevelData`, `SuperIZLevelData`, plus a Zuma clone (`ZumaBall`) and a fruit-ninja clone
(`FruitNinjaManager`, `FruitBuffType` with 25 entries) (tier A).

### 3.10 Travel mode — the roguelite trait draft

**FACT (tier A).** The trait system is enum-backed and large:

| Enum | 3.8.1 | 3.9 | Growth |
|---|---|---|---|
| `AdvBuff` (advanced player traits) | 154 | **177** | +23 |
| `UltiBuff` (ultimate player traits) | 54 | **56** | +2 |
| `InvestBuff` (investment traits) | 42 | **42** | 0 |
| `TravelDebuff` (zombie-side traits) | 91 | **142** | **+51 (+56% in one minor version)** |

`Il2Cpp.BuffType` = `UnlockPlant, AdvancedBuff, UltimateBuff, Debuff, InvestmentBuff`.
`Il2Cpp.TravelDifficulty` = `Normal, Hell, Curse, Invest`. `Il2Cpp.TreasureDifficulty` = `Normal, Hard, Hell,
Upgrade, Fate`. `Il2CppGameLevel.Abyss.ZombieBuff` = `None, Health1–3, Damage1–3, Speed1–3` — **tiered authored
buckets, not a continuous multiplier** (all tier A).

**FACT (tier B/C).** How the draft plays:

- Travel splits into 经典旅行/旅行·生存 (classic), 旅行冒险 (adventure), 旅行游戏 (games), 旅行挑战 (challenge).
  Variants include 旅行·守护 (an Unbreakable analogue), 旅行·轮回 (endless inside travel) and 旅行·炼狱 (a
  harder survival).
- **Every round you pick one player trait and are forced to also pick one zombie trait.** At rounds 3/6/9 you
  additionally pick a strong-ultimate plant *and* are forced to take an ultimate leader zombie. **You get
  exactly three leader zombies per run and cannot refuse them.**
  ([3DM 旅行词条 guide](https://shouyou.3dmgame.com/gl/574573.html), tier C.)
- **Investment traits (投资词条): one per scene.** Taking a gold- or iridescent-quality investment trait raises
  the run's inherent difficulty — higher-tier zombies appear more often, and zombies gain extra damage
  reduction. A deliberate risk-for-power trade. (Moegirl via search index, tier B.)
- **In 旅行·炼狱 (Hell travel), zombie HP is ×1.3 in round 1 and then +30% per round** — a geometric HP curve.
  ([3DM](https://shouyou.3dmgame.com/gl/574573.html), tier C.) **This is where HP inflation lives; the main
  difficulty ladder deliberately avoids it.**
- **Abyss S2** is a 30-level run where you start with abundant sun but cannot generate it; **plants carry over
  between levels**, and you draft an Abyss token after each stage.
  ([BWIKI 3.1版本](https://wiki.biligame.com/pvzrh/3.1版本), tier B.)

Note the trait-quality vocabulary — 黄金 (gold) and 棱彩 (iridescent) — matches `UI.Quality`'s `gold` and
`iridescent` members exactly. **The rarity vocabulary is shared across plants, upgrades and traits.**

### 3.11 The in-match economy

**FACT (tier B, [BWIKI 基本玩法](https://wiki.biligame.com/pvzrh/基本玩法)).**

- **Fertiliser (肥料)** unlocks at Adventure stage 13, regenerates **every 70 seconds**, and one application does
  one of several context-dependent things: full-heal every plant in a tile, upgrade a specified basic plant to
  its purple version, advance a growth stage, finish a carnivorous plant's digestion, reload a cannon, produce a
  drone/mine/summon, or clear burn status. **One button, ~7 authored meanings, chosen by target.**
- **Zombie fertiliser** (3.8+) auto-applies to a random plant every **5 seconds**, inflicting a "burn lockdown"
  that takes **25 projectile hits** to clear.
- **Gold coffee bean:** **1,000 coins = 1 bean**. A bean upgrades a silver plant to gold, granting one activated
  ultimate ability plus one instant-cooldown trigger.
- **Grand Garden (大花园, added 3.0):** a separate garden with its own currency (神秘模式钱币). Devices cost
  **100,000** each and project an area effect that grows from **3×3 at level 1 to 7×7 at level 3**. Plants have
  **durability**: a watering can restores 1 point to one plant; fertiliser raises max durability by **20** and
  restores **20**; pesticide and phonograph restore fully.
  ([BWIKI 大花园](https://wiki.biligame.com/pvzrh/大花园), tier B.)

### 3.12 Version history

**FACT (tier B, [BWIKI 融合版时间线](https://wiki.biligame.com/pvzrh/融合版时间线)).** Headline per release:

| Version | Date | Headline |
|---|---|---|
| 1.0 | 2024-06-30 | Day mode — initial release |
| 1.2 | 2024-07-22 | Night mode; I-Zombie mode |
| 2.0 | 2024-08-14 | Pool mode; Adventure, Random, mobile support |
| 2.1.2 | 2024-09-30 | Fog mode; **Custom mode** |
| 2.1.4 | 2024-11-10 | Roof (upper) |
| 2.1.5 | 2024-12-07 | Roof (middle); garden expansion; HP display |
| 2.2 | 2025-01-25 | Roof complete; **Leader and Inferno difficulties**; shop; crossover |
| 2.3 | 2025-03-01 | Ultimate-difficulty experience levels; classic adventure rewards |
| 2.4 | 2025-04-11 | **Abyss Season 1**; ultimate-plant skins and reworks |
| 2.5 | 2025-04-29 | Snow route; major balance pass |
| 2.6 | 2025-05-30 | **Inferno Curse mode** |
| 2.7 | 2025-07-05 | Anniversary; **Evolution difficulty; Gods: Evolution mode** |
| 2.8 | 2025-08-09 | **Gods: Shooting mode**; tower levels reworked |
| 3.0 | 2025-09-28 | **Grand Garden**; Mystery mode |
| 3.1 | 2025-11-12 | **Abyss Season 2**; travel routes |
| 3.2 | 2025-12-21 | **Codex system overhaul**; cricket-fighting mode; fusion priority reorder |
| 3.3 | 2026-01-17 | **Starlight Adventure** |
| 3.4 | 2026-02-10 | **Travel Investment mode** |
| 3.5 | 2026-03-22 | **Super Custom levels**; Easy Adventure; save system |
| 3.6 | 2026-04-26 | Rhythm-master editor; **mechanic codex and fusion-codex unlock system** |
| 3.7 | 2026-06-07 | **Curse mechanic overhaul**; term codex; cart item; **30 new plants, 10 new zombies** |
| 3.8 | 2026-07-12 | Second anniversary; Zen Garden rework; UI customisation; **14 + 8 plants, 3 + 6 zombies** |
| 3.9 | 2026-08-15 | **Gods: Inferno evolution**; snow route reset; ballistics refactor; **16 + 4 plants, 2 + 2 zombies** |

Per-version content counts come from the individual BWIKI version pages
([3.7](https://wiki.biligame.com/pvzrh/3.7版本), [3.8](https://wiki.biligame.com/pvzrh/3.8版本),
[3.9](https://wiki.biligame.com/pvzrh/3.9版本)), all tier B; the 3.9 figure is independently confirmed by the
binary diff in §3.2.

**INFERENCE (mine).** Read the timeline as a shape and one thing jumps out: **the first year was boards, the
second year was modes.** 1.0–2.1.5 shipped the vanilla lawn set (day, night, pool, fog, roof). From 2.2 onward
almost every release is a new *mode* or a new *meta-layer* — difficulties, Abyss seasons, Gods, Grand Garden,
Travel investment, Starlight, custom levels with online sharing. Plants kept arriving at ~15–30 per release the
whole time, but they stopped being the headline. Also note 3.6's "fusion-codex unlock system": **the recipe book
became a progression object two years in**, not at launch.

### 3.13 Mechanic-by-mechanic: problem, cost, failure

| Mechanic | Problem it solves for the player | What it costs the designer | What breaks when tuned wrong |
|---|---|---|---|
| **Pairwise in-match fusion** | Turns a fixed 6–10 card deck into a combinatorial space. Every match is a puzzle about what your cards *become*, not what they are. | Authored art, stats, behaviour and a wiki entry for **every** result. 697 plant types is the bill. Combinatorial explosion is not free — it is 697 hand-made things. | If results are not clearly better than parents, fusion is a trap. If they are always better, never fusing is never correct and the base cards are decoration. |
| **Recursive tree with `Depth`** | Long-run mastery: a player can chase a 4-deep result across a whole match. | Every intermediate must be independently playable, because you will stand on it for 30 seconds. **No filler rungs allowed.** | Deep results that need specific mid-tier plants become "you lost at card select". Depth converts build diversity into build lock-in. |
| **Multiple paths to a result (`GetMixPaths`)** | Redundancy — a bad card draw does not lock a target out. | Every extra path is a balance surface: the cheapest path becomes the only path. | Converges to one dominant route and the rest is dead data. |
| **Un-fusion (`TryGetDisMix`)** | An undo. Removes the fear that makes players hoard and never fuse. | Must not be a free reroll — needs a cost or a restriction, or it trivialises commitment. | Free un-fusion turns the board into a scratchpad and deletes the decision. |
| **Randomised recipe table (`_recipes_random`)** | Replayability without new content. The same 88 base cards produce a different puzzle. | Needs the whole result space to be *legible* under randomisation, or the mode is noise. | If random recipes can produce unreachable or useless results, the mode is a slot machine with a lawn attached. |
| **Fusion priority order** | Removes ambiguity when one click could plant, fuse, pumpkin or pot. | A rule players must learn that teaches them nothing about the game. | The 3.2 pumpkin/pot swap is the tell: get this wrong and players lose plants to misclicks. It is invisible when right and infuriating when wrong. |
| **Rarity as a planting budget (Abyss)** | Makes rarity mean something in play, not just in a collection screen. | Every plant now needs a rarity that is honest about its power, across every mode that reads it. | Mis-rate one plant and it is either banned-by-budget or a free win. |
| **Zombie gear as fusion material** | Makes the enemy a resource. Killing is not the only interaction. | Every armour piece needs a magnetism rating, a class, a stolen-form and a consume-effect — four properties per item. | Steal too cheaply and armour stops being a threat; too expensively and the whole magnet archetype is dead weight. |
| **Difficulty as spawn count + damage reduction** | Higher difficulty stays *readable*. A 10× wave looks like a 10× wave. | Ten times the entities is ten times the simulation cost, and the game must stay above frame budget. | ×10 spawn with 60% damage reduction is a soft HP multiplier anyway — the two knobs multiply. Tune both up together and the wall is invisible until it is vertical. |
| **Forced zombie trait per round (Travel)** | Guarantees the run gets harder even when the player drafts perfectly. Removes the "I picked well so nothing happened" outcome. | The zombie trait pool has to stay interesting for a whole run — 142 entries in 3.9, up 56% from 3.8.1. | If zombie traits outpace player traits, every run ends at the same round and drafting stops mattering. |
| **Investment traits (power now, harder run)** | Player-authored difficulty. Lets a strong player opt into a real fight. | Needs the difficulty side to actually bite, or it is just free power. | One-sided value here quietly deletes the base difficulty setting. |
| **Geometric HP in Hell travel (+30%/round)** | A clean, legible run length: everyone knows roughly where the wall is. | Exponential curves meet integer limits fast, and every player-side multiplier now compounds against it. | Set the ratio slightly high and the mode has a hard round number nobody passes; slightly low and it never ends. |

---

## 4. Chinese-exclusive worlds and mode structure (PvZ2)

**FACT (tier B, [plantsvszombies.wiki.gg — 植物大战僵尸2（中文版）](https://plantsvszombies.wiki.gg/zh/wiki/植物大战僵尸2（中文版）)).**
The Chinese PvZ2 lists **17 main worlds**:

神秘埃及 · 海盗港湾 · 狂野西部 · **功夫世界** · 未来世界 · 黑暗时代 · 巨浪沙滩 · 冰河世界 · **天空之城** ·
失落之城 · 摇滚年代 · 恐龙危机 · 摩登世界 · **蒸汽时代** · **复兴时代** · **平安时代** · **海底世界**

**INFERENCE (mine, computed by set difference against the international world list).** Eleven of those map to
international worlds (Ancient Egypt, Pirate Seas, Wild West, Far Future, Dark Ages, Big Wave Beach, Frostbite
Caves, Lost City, Neon Mixtape Tour, Jurassic Marsh, Modern Day). The remaining **six are Chinese-only**: Kung
Fu World, Sky City, Steam Ages, Renaissance Age, Heian Age, Undersea World. Plus 童话森林 (Fairytale Forest) and
创意庭院 (Creative Yard, a level editor) as non-world content.

**Kung Fu World is directly confirmed Chinese-exclusive** on the English wiki ("Kongfu World … Chinese-exclusive"
— [wiki.gg search](https://plantsvszombies.wiki.gg/index.php?search=Kung-Fu+World+Chinese+exclusive), tier B),
and is the one most often cited in CN-vs-intl comparisons ([TapTap](https://www.taptap.cn/moment/427773268418626266),
tier C). The other five are my set difference and are **inference, not confirmed one by one.**

**FACT (tier B).** Mode structure around the worlds:

| Mode | Chinese | Shape |
|---|---|---|
| Adventure | 冒险 | The world-by-world campaign |
| Endless | 无尽挑战 / Endless Zone | Per-world endless variants; difficulty escalates by **board hazard density**, e.g. Egypt's endless fills up to **80%** of the lawn with gravestones, and Frostbite's chilling winds arrive more often |
| Fragment challenge | 碎片挑战 | Daily, drops plant fragments — the fragment tap that feeds §1.2 |
| Competitive league | 超Z联赛 | Scored ladder |
| Penny's Pursuit | 潘妮的追击 | Rotating challenge run |
| Creative Yard | 创意庭院 | Player level editor |
| Memory trip | 回忆之旅 | PvZ1 plants |
| Co-op / duel | 双人对决 | Two-player |
| Escape | 僵局逃脱 | Event |

Sources: [wiki.gg 中文版](https://plantsvszombies.wiki.gg/zh/wiki/植物大战僵尸2（中文版）) and
[BWIKI pvzz](https://wiki.biligame.com/pvzz/植物大战僵尸2中文版) (both tier B).

**INFERENCE (mine).** Endless in PvZ2 — both versions — is not a multiplier ramp. It is **authored hazard
density plus a generated board layout**. That is a materially different design from Fusion's Travel/Abyss, where
the ramp is explicit percentages. Neither is obviously better; the PvZ2 approach costs more authoring and
produces more variety per round, the Fusion approach is a single tunable and produces a predictable wall.

---

## 5. Monetisation-driven systems, described as mechanics

**FACT (tier B/C).** The Chinese build's economy is the reason its progression exists at all. The load-bearing
parts:

| System | Chinese | Mechanic |
|---|---|---|
| **Plant-specific fragments** | 碎片 | The atomic progression currency. **Not fungible across plants** (there is a separate "universal fragment" item, which player guides advise *against* spending on tier-ups — [BWIKI pvzz search](https://searchwiki.biligame.com/pvzz/index.php?search=碎片&fulltext=1), tier B). Sources: chests, Penny's shop, endless, challenge modes. |
| **Chests / gacha** | 宝箱, 许愿池 | Diamonds open chests; chests drop fragments. This is the randomised layer the international version does not have. |
| **Premium currency** | 钻石 | Buys chests and shop stock. In the Chinese build diamonds can also **buy sun and Plant Food mid-match**; in the international build sun cannot be bought at all. Chinese acquisition channels for diamonds were **reduced** relative to international ("缩减钻石获取渠道"). |
| **Direct plant purchase** | 充值 | Some plants list acquisition as literally "**充值98元RMB**" — recharge ¥98 ([wiki.gg PvZ2C:植物](https://plantsvszombies.wiki.gg/zh/wiki/PvZ2C:植物), tier B). |
| **Coins** | 金币 | The bulk sink: **850,000 coins to take one plant to tier 5** (computed, §1.2). |
| **Coloured culture fluid** | 培养液 | A second, colour-matched gate on the final tier only — **10 bottles of the plant's own quality colour**. A hard rarity-scoped sink. |
| **Costumes** | 装扮 | Stat-bearing in the Chinese build. Also feed family level (+1 each). |
| **Family collection** | 家族 | Converts breadth of ownership into a stat roll, with **3 free rerolls per day** — a daily-return hook attached to a collection metric. |

Sources: [BWIKI pvzz](https://wiki.biligame.com/pvzz/植物大战僵尸2中文版), [TapTap](https://www.taptap.cn/moment/427773268418626266),
[7723](https://www.7723.cn/strategy/27709.html), [wiki.gg](https://plantsvszombies.wiki.gg/zh/wiki/PvZ2C:植物).

**Publisher note (tier B).** The Chinese build passed from 上海宝开 (PopCap Shanghai) to **拓维 (Talkweb)**, and
community write-ups tie the harder fragment/tier economy to that handover.

**INFERENCE (mine).** Three of these are RPG-shaped in a way worth naming precisely:

1. **Plant-specific fragments make every drop a named drop.** The design win is that no reward is generic. The
   design cost is a duplicate problem, which the game answers with a universal-fragment item that players are
   then advised not to use — i.e. the answer is bad and everyone knows it.
2. **The colour-matched culture fluid gate is a rarity-scoped sink.** It means the last tier of an orange plant
   and the last tier of a white plant draw from different pools, so you cannot bank one currency and dump it.
3. **Family level converts collection into stats with a daily reroll.** It is the only system here that rewards
   owning things you never play, and it is also the only one with a per-day cadence.

**Mechanic, cost, failure:**

| Mechanic | Player problem solved | Designer cost | Failure when mistuned |
|---|---|---|---|
| Plant-specific fragments | Every reward has a name and a destination | Per-plant drop tables; a duplicate sink; a shop that stocks the right ones | Documented failure: high-tier fragments are "hard to get and highly random", players stall on one unit |
| Gacha chests | Compresses a long grind into a short session | Needs pity/floor mechanics or the variance eats new players | Without a floor, median progress ≪ advertised progress |
| Rarity-matched final-tier gate | Stops one currency from trivialising every plant | A second inventory axis with its own drop rate | Under-supply one colour and an entire rarity band is frozen at tier 4 |
| Family level from ownership | Makes breadth pay | A second economy that must stay subordinate to the first | If breadth beats depth, the fragment loop it funds becomes irrational |
| Daily free rerolls (3×) | A cheap reason to open the app | Rerolls must matter enough to want and little enough not to gate | Too strong and the game is a slot machine you visit; too weak and nobody rerolls |

---

## 6. Chinese zombie roster and difficulty scaling

**FACT (tier A, PVZ Fusion).** 228 `ZombieType` entries in 3.9, 225 in 3.8.1 (computed). Scaling on the main
difficulty ladder is **spawn count (×1 to ×10), speed (+0 to +0.2, band 1.0–1.4), damage reduction (0 / 30% /
60%), and at N6 max toughness +40%** — see the table in §3.8. **No HP multiplier on the base ladder.**

**FACT (tier A, PVZ Fusion).** Where HP scaling does exist, it is either an authored bucket or an explicit
percentage:

- `Abyss.ZombieBuff` = `Health1, Health2, Health3, Damage1–3, Speed1–3` — **three authored steps per axis**, not
  a continuous curve.
- `TowerBuff` = `Damage_1..3, Speed_1..3, Startsun_1..3` — same three-step shape on the player side.
- Hell travel: **×1.3 in round 1, then +30% per round** (tier C).

**FACT (tier A, PVZ Fusion).** The per-board multiplier surface this project already hooks —
`zombieHealthMultiplier`, `zombieDamageMultiplier`, `zombieSpeedMultiplier`, `zombieCountMultiplier`,
`zombieStartAmmor`, `plantModifyMin/Max`, `zombieModifyMin/Max`, `waveInterval`, `conveyInterval`
([`game-types-381.md`](../game-types-381.md)) — confirms that the engine *has* continuous multipliers on all
four axes. **The difficulty ladder chooses not to use the HP one.**

**FACT/INFERENCE (Chinese PvZ2).** I could not find a numeric zombie-scaling table for the Chinese PvZ2. What
the wikis do say: zombie descriptions are relative and authored ("交通锥木乃伊僵尸 … twice as sturdy as a normal
mummy"), and the endless zones scale by **hazard density and board generation**, not by stat multipliers
([wiki.gg PvZ2C:僵尸](https://plantsvszombies.wiki.gg/zh/wiki/PvZ2C:僵尸) — page marked 正在施工中, under
construction; and the endless-mode search results, both tier B). **My inference: PvZ2 uses authored per-level
zombie tables and authored hazards rather than global multipliers.** This is consistent with PvZ2's known level
format but I did not verify it against game data. Treat as unconfirmed.

**The comparison worth keeping:**

| | Authored tables (PvZ2) | Global multipliers (Fusion difficulty) | Percentage ramps (Fusion Travel/Abyss) |
|---|---|---|---|
| Variety per unit of effort | High | None | Low |
| Cost per new difficulty | One authored level set | One number | One number |
| Predictability of the wall | Low | High | Very high |
| What breaks | A single mis-authored level blocks everyone | The wall is invisible until vertical | Exponential meets integer limits |

---

## 7. The other Chinese PvZ titles

### 7.1 PvZ Online (植物大战僵尸Online) — the deepest RPG layer in the family

**FACT (tier B, [plantsvszombies.wiki.gg — Plants vs. Zombies Online](https://plantsvszombies.wiki.gg/wiki/Plants_vs._Zombies_Online)).**

- Developer: **PopCap Shanghai + Tencent Games**; publisher Tencent. China-only, QQ account required.
- Betas **2013-12-09** and **2014-07-18**; **shut down 2018-08-11**.
- Worlds: Qin Shi Huang Mausoleum (40 levels), Ancient Egypt (30), Pirate Seas (60), Far Future (60), East Sea
  Dragon Palace (16, unfinished).
- **Three different game modes in one product:** a tower-defence Normal mode, an **Adventure mode that is
  turn-based RPG combat with plant decks**, and a **Map mode with base placement and resource harvesting**.
- Plant progression: **star ratings** (examples show up to 3 stars), **Plant Awakening (觉醒)** which raises a
  plant into a "moon" rarity band, level upgrades run from the Player's House, a **Greenhouse** (unlocked at
  player level 5) for upgrades, a **Nutrition Room** where plants take stat-improving reagents, a **Tree of
  Wisdom**, and a **Laboratory** for experiments. Seed-packet colour encodes the number of upgrades applied.
- Monetisation: **Gold Gems** premium currency, **VIP membership tiers**, recharge events, and spend-rebate
  campaigns.
- The Greenhouse article explicitly frames itself as "similar to the upgrade system in the Chinese version of
  Plants vs. Zombies 2" ([Greenhouse](https://plantsvszombies.wiki.gg/wiki/Greenhouse), tier B).

**This is the title that actually answers "what does a full RPG layer on a lawn look like".** It is also the one
whose documentation rotted hardest — the wiki pages are archived stubs.

### 7.2 PvZ Great Wall Edition (植物大战僵尸长城版)

**FACT (tier B, [wiki.gg](https://plantsvszombies.wiki.gg/wiki/Plants_vs._Zombies:_Great_Wall_Edition)).**
PopCap Shanghai, released **2012-05-18**. A heavy reskin of PvZ1: re-skinned zombies, a "China Pavilion" mode,
and **a new Mall feature**. Its content is almost entirely **endless survival variants** — Survival: Great Wall,
Great Wall Endless, Great Wall Night Endless (iOS), Great Wall Pool Endless (iOS), Great Wall Fog Endless
(Android), Last Stand: Great Wall Endless (Android), Boss Rush Endless (Android). Later updates folded in
Journey to the West episodes. Delisted when support ended.

**INFERENCE (mine).** No stat or level layer. Its whole answer to "what do players do after the campaign" was
**more endless variants plus a shop**. It is the null hypothesis in this file: the version that added a store
and no progression.

### 7.3 PvZ Journey to the West (植物大战僵尸：西游版)

**FACT (tier B, [wiki.gg](https://plantsvszombies.wiki.gg/wiki/Plants_vs._Zombies:_Journey_to_the_West)).**
PopCap Shanghai + Tencent + China Mobile, **2013-02-01**. **Five adventure worlds × 13 levels.** New
themed plants (Wukong Pea, Monk Flower, Nezha Shooter, Iron Man Nut) and zombies (Flying Imp). Progression is a
**star-based objective system** — trophies and money bags for meeting per-level goals (kill counts, mowers
preserved). Discontinued 2015–2016; final version 40.10.

**INFERENCE (mine).** Star-per-objective is the cheapest progression in this file: no stats, no currency sink,
no per-unit authoring — just three named goals per level. It buys replay of content you already made.

### 7.4 PvZ Social (植物大战僵尸社交版)

**FACT (tier B, [wiki.gg](https://plantsvszombies.wiki.gg/wiki/Plants_vs._Zombies_Social)).** PopCap Shanghai,
on the **Renren** social network, **July 2011 → 2014-09-30**. Added **leaderboards and a town-building layer
where friends visit your town**, 8 exclusive plants and 3 exclusive zombies. Details of its level/energy/currency
systems are not documented on the wiki.

### 7.5 "PvZ Toy edition"

I could not identify a title matching this name — see "What I could not find".

---

## What I could not find

Every item here was searched for and not obtained. Chinese sources rot fast and several gate on login.

**Hard blocks (source exists, could not be read):**

1. **Moegirl (萌娘百科) returns HTTP 403 to every direct fetch**, on `zh.moegirl.org.cn`, the `mzh.` mirror, and
   the MediaWiki `api.php` endpoint. Its `植物大战僵尸2/中文版植物强化` and `植物大战僵尸融合版` pages are the
   best-structured sources for both games. Everything attributed to Moegirl in this file came through a search
   index summary, not the page itself — **treat those numbers as one step weaker than the rest.**
2. **Zhihu (`zhuanlan.zhihu.com`) returns 403.** Two detailed free-to-play economy guides
   (`/p/564155337` — PVZ2 中文版不氪金攻略, `/p/537996376`) were unreachable directly; only the second's
   tier-2/3 costs survived via a search snippet.
3. **Baidu Baike returns 403** for both `植物大战僵尸融合版` and `植物大战僵尸Online`.
4. **Fandom's Chinese PvZ wiki (`pvz.fandom.com/zh`) returns HTTP 402** on every page tried.
5. `www.tolyg.com` and `www.gamedog.cn` — connection refused / reset. Both held ultimate-plant recipe lists.
6. **Web search quota was exhausted mid-run** (200/200). The second half of this research was done by fetching
   known URLs and by driving MediaWiki `Special:Search` on the wikis directly. Whole query families were never
   run — in particular a proper Tieba (百度贴吧) and Bilibili-article sweep, which is where datamined tables
   usually live.

**Facts I wanted and do not have:**

7. **The actual number of fusion recipes in PVZ Fusion.** The game computes it —
   `PlantMixTreeManager.MixTreeStatistics.TotalMixRecipes` — but the recipe table is built by native IL2CPP code
   in `GameAssembly.dll`, so it is not readable from the managed interop assembly. It would take a running game
   and a call to `PrintAllStatistics()` / `GetRecipes()`. Same for **`MaxTreeDepth`**, **`BasicPlantCount`**,
   **`PlantWithMostChildren`** and **`MaxChildrenCount`**.
   **Partially answered after this file was written, from a tier-B source** — the English Fusion wiki
   states **532 fusions in v3.9** (514 excluding Infusible): 323 Common (2 base plants), 77 Upgraded,
   12 Advanced (3 base plants, gated behind a Fusion Challenge), 102 Odyssey, 18 Infusible (no formula —
   only spawned by other fusions), plus Titan. See
   [`03-pvz2-mods.md` §2](03-pvz2-mods.md) and <https://pvzfusion.wiki.gg/wiki/Fusions>.
   **This is a wiki tally, not the engine's own `TotalMixRecipes`**, and it counts *results* rather than
   recipes — `GetMixPaths` proves one result can have several routes, so the recipe count is a lower
   bound of 532, not an equality. The tier-A number still needs a running game.
8. **Whether a fused plant inherits current HP, buffs or statuses from its parents.** The BWIKI search for
   `融合 血量 继承` returns no results. My §3.3 inference (authored stats via `SetPlantAttributes`, no live-stat
   blend) is reasoned, not confirmed.
9. **A recipe list in the form A + B = C** from any source. The BWIKI 融合配方 page is an image-based navigation
   hub; the category tables list plant properties but not their recipes. Individual plant pages presumably carry
   them, which is ~490 fetches.
10. **Counts for 衍生植物 (derivative plants), 塔防植物 (tower-defence plants) and 武器防具 (weapons & armour)**
    in PVZ Fusion. Those category pages were not fetched.
11. **The `AdvBuff` / `UltiBuff` / `InvestBuff` / `TravelDebuff` member names.** The counts are solid but Cecil
    could not resolve the constants for those specific enums (they came back as unnamed serialized constants),
    so I have the sizes of the trait pools and none of their contents.
12. **The exact fusion tree depth and whether ordered recipes are common or a handful of special cases.**
    `AddOrderedRecipe` exists; I do not know how many recipes use it.
13. **Whether the Chinese PvZ2 has a stamina/energy (体力) system or VIP tiers.** One wiki summary asserted both;
    no other source I reached mentions either, and the detailed CN-vs-intl comparison does not list them.
    **Unverified — do not repeat it as fact.** (VIP tiers in **PvZ Online** are confirmed; that may be the
    source of the confusion.)
14. **Numeric zombie health/damage tables for the Chinese PvZ2.** The wiki.gg zombie page is explicitly under
    construction and carries no stat columns.
15. **Any first-party developer statement** about either game. No patch notes from Talkweb, no design post from
    蓝飘飘fly. Everything in §1–§7 that is not the binary is community-reconstructed. There is a `changelog.txt`
    in the local 3.8.1 pack but it belongs to the mod tool, not the game (56 bytes, "0 changes since").
16. **A "PvZ Toy edition" (玩具版).** No title by that name surfaced. Candidates it might refer to: PvZ Social
    (Renren), PvZ Adventures (Facebook, not China-specific), or a physical toy line. **Unresolved.**
17. **Release dates for PVZ Fusion 1.0–2.1.5 beyond what the timeline page gave** — the timeline is complete on
    dates, but I have no second source for the pre-2.5 entries.
18. **The Chinese-exclusivity of five of the six worlds in §4.** Only Kung Fu World is directly sourced; Sky
    City, Steam Ages, Renaissance Age, Heian Age and Undersea World are my set difference.
19. **What "gold coffee bean" upgrades cost in aggregate**, and how the `Quality` enum's `diamond`, `curse` and
    `iridescent` states are obtained. Only the silver→gold step (1,000 coins → 1 bean) is documented.
20. **`PVZRHTools` datamined tables.** Four forks exist (CarefreeSongs712, allenzhang710901, MKHkro1,
    Infinite-75) and one is already referenced in this repo's `sources.md`, but I did not open their sources —
    and this repo's rules forbid pasting foreign source into the tree, so any use of them would have to stay at
    the level of "this tool exposes X".

---

## Hooks for this project

**Non-normative and un-vetted.** These are observations about where a mechanic in this file rhymes with
something this repo already has. None of them is a recommendation, none has been checked against the design
gate, and several may already be solved or already rejected.

- **`PlantMixTreeManager.MixTreeStatistics`** — the host game computes `TotalMixRecipes`, `MaxTreeDepth`,
  `BasicPlantCount`, `PlantWithMostChildren`, `MaxChildrenCount` over its own recipe graph. That is very close to
  the roster-metric shape a generator would want for a demon fusion tree.
- **`AddRecipe` vs `AddOrderedRecipe`** — the host game found it necessary to have both an unordered and an
  order-sensitive recipe form. A fusion system that assumes commutativity is making a choice, not observing one.
- **`TryGetDisMix` (un-fusion)** — the host game ships a decompose-back-to-parents path with its own
  `Plant.DieReason.ByDisMix`. Reversibility was worth building.
- **`_recipes_random` + `UpdateRandomMix`** — the same content generates a different puzzle when the recipe table
  is re-rolled at runtime. That is replayability without new authoring.
- **`CardLevel` (White/Green/Blue/Purple/Gold/Red) is a shipped six-rung rarity ladder** in the host game, and
  the same vocabulary (`gold`, `iridescent`) reappears in `UI.Quality` and in Travel trait qualities. There is an
  existing rarity language on this lawn.
- **Rarity as a planting budget in Abyss mode** — rarity gates how many of a unit may be on the board at once,
  rather than only what it costs.
- **Difficulty as spawn count + damage reduction, never HP** — the host game's own N0–N6 ladder deliberately
  leaves `zombieHealthMultiplier` alone even though the engine exposes it.
- **Zombie weapons and armour as player-harvestable fusion material**, with a per-item magnetism rating deciding
  who can take it. Enemy gear as a resource, with a stealing-difficulty stat.
- **Forced adverse draft (one zombie trait per round, three unrefusable leaders per run)** — the run gets harder
  on a schedule regardless of how well the player drafts.
- **Investment traits: opt into higher difficulty in exchange for power, priced by trait rarity.**
- **`TravelDebuff` grew 91 → 142 in one minor version while `InvestBuff` stayed at 42** — the enemy-side trait
  pool is where this game spends its content budget.
- **Chinese PvZ2's tier ladder is 1.5× / 2× / 2.5× / 3× on both ATK and HP** — a flat linear multiplier per rung,
  with the interesting part carried entirely by an authored per-tier skill rider.
- **Plant-specific fragments** make every drop a named drop, at the cost of a duplicate problem the game never
  really solved.
- **The colour-matched culture-fluid gate on the final tier only** — a currency scoped to the unit's own rarity
  band, so one stockpile cannot max everything.
- **Family level: ownership breadth (plants + tiers + costumes) sets the ceiling of a randomly rolled stat, with
  three free rerolls a day.** Collection converted into a stat roll with a daily cadence.
- **PvZ Online's building-per-system layout** — Greenhouse for levels, Nutrition Room for stats, Laboratory for
  experiments, Tree of Wisdom — is one way to keep four progression systems legible: give each one a place.
- **PVZ Fusion's version history says the first year was boards and the second year was modes**, and the fusion
  codex only became a progression object at 3.6, two years in.
